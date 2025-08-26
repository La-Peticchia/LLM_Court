using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LLMUnity;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Court : MonoBehaviour
{
    //References
    [Header("References")]
    [SerializeField] private LLMCharacter llmCharacter;
    [SerializeField] private InputField playerText;
    [SerializeField] private TextMeshProUGUI aiText;
    [SerializeField] private Text aiTitle;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI systemMessages;
    [SerializeField] private TextMeshProUGUI caseDescriptionText;
    [SerializeField] public Button nextButton;
    private APIInterface _apiManager;
    private SentenceAnalyzer _sentenceAnalyzer;
    //[SerializeField] RunJets runJets;
    [SerializeField] public Button micButton;
    public MicrophoneInput micInput;
    [SerializeField] private CharacterAnimator characterAnimator;
    [SerializeField] private EndGameUI endGameUI;
    [SerializeField] private TextMeshProUGUI playerGoalText;
    private CourtRecordUI _courtRecordUI;
    private SettingsUI _settingsUI;

    [Header("TTS Integration")]
    [SerializeField] private KokoroTTSManager ttsManager;
    [SerializeField] private bool enableTTS = true;
    [SerializeField] private bool waitForTTSCompletion = true;

    //Names
    [Header("Character Names")]
    [SerializeField] private string wildcardCharacterName = "Wildcard";
    [SerializeField] private string defenseName = "Defense";
    [SerializeField] private string attackName = "Prosecutor";
    [SerializeField] private string judgeName = "Judge";

    //Prompts
    [Header("Character Prompt")]
    [TextArea(5, 10)] public string mainPrompt = "A court case where the AI takes control of several characters listed below";
    [TextArea(5, 10)] public string judgePrompt = "The goal of Judge is to give the defendant's final sentence by listening to the dialogue";
    [TextArea(5, 10)] public string attackPrompt = "The goal of Prosecutor is proving to the Judge that the defendant is guilty";
    [TextArea(5, 10)] public string witnessesPrompt = "Witnesses";

    //Command text
    private readonly string _questionCharacter = "<";
    private readonly string _interventionGrantCharacter = "[";
    private readonly string _requestCharacter = "*";
    private readonly string _gameOverCharacter = "#";
    private readonly string _languagePlaceholder = "<language>";
    private readonly string _judgePlaceholder = "<Judge>";
    private readonly string _prosecutorPlaceholder = "<Prosecutor>";
    private readonly string _witnessesPlaceholder = "<Witnesses>";


    //Gameplay
    [SerializeField, UnityEngine.Range(1, 5)] private int numOfInteractions;
    private int _defenseInteractions;
    private int _attackInteractions;
    [SerializeField] private bool enableAnalyzeInfo = true;

    public bool PlayerCanAct => _roundsTimeline[_round].role == defenseName;

    //State track
    private List<(string role, string systemMessage)> _roundsTimeline;
    private CaseDescription _caseDescription, _translatedDescription;
    private int _round;
    private bool IsOutOfQuestions => _attackInteractions <= 0 && _defenseInteractions <= 0;
    private bool _lastPhase;

    //End game message
    private (string message, Color color)? _pendingEndGameMessage = null;
    private readonly string _winTag = "#WIN";
    private readonly string _lossTag = "#LOSS";


    //Events
    public event Action<bool> GameOverCallback;
    private bool _finalRoundStepOneDone = false;
    private string _finalRoundSummary = null;




    private void Awake()
    {
        _defenseInteractions = _attackInteractions = numOfInteractions;

        _apiManager = FindFirstObjectByType<APIInterface>();
        _sentenceAnalyzer = FindFirstObjectByType<SentenceAnalyzer>();

        if (ttsManager == null)
            ttsManager = FindFirstObjectByType<KokoroTTSManager>();

        playerText.onSubmit.AddListener(OnInputFieldSubmit);
        nextButton.onClick.AddListener(OnNextButtonClick);

        llmCharacter.playerName = defenseName;

        _courtRecordUI = FindFirstObjectByType<CourtRecordUI>();
        enabled = false;

        if (_settingsUI == null)
            _settingsUI = FindFirstObjectByType<SettingsUI>();

    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (IsAnyInputFieldFocused()) return;
            if (_settingsUI != null && _settingsUI.IsOpen) return;

            if (!_roundsTimeline[_round].role.ToLower().Contains(defenseName.ToLower()) && nextButton != null && nextButton.interactable)
            {
                OnNextButtonClick();
            }
        }
    }

    private static bool IsAnyInputFieldFocused()
    {
        if (EventSystem.current == null) return false;
        var go = EventSystem.current.currentSelectedGameObject;
        if (go == null) return false;
        return go.GetComponent<TMP_InputField>() != null || go.GetComponent<InputField>() != null;
    }

    public async void InitializeCourt(CaseDescription caseDescription, CaseDescription translatedDescription)
    {
        _caseDescription = caseDescription;
        _translatedDescription = translatedDescription;
        enableTTS = translatedDescription.language.ToLower().StartsWith("en");
        
        if (playerGoalText)
            playerGoalText.text = _translatedDescription.GetTotalDescription(1, true);

        mainPrompt = mainPrompt.Replace(_judgePlaceholder, judgePrompt).Replace(_prosecutorPlaceholder, attackPrompt).Replace(_witnessesPlaceholder, witnessesPrompt).Replace(_languagePlaceholder, translatedDescription.language);

        InitializeChat();
        InitializeRounds();

        caseDescriptionText.text = _translatedDescription.GetTotalDescription(true);
        characterAnimator.AssignDynamicPrefabs(_translatedDescription.witnessNames,
                                             _translatedDescription.witnessGenders,
                                             attackName);

        List<string> characters = new List<string> { judgeName, defenseName, attackName };
        characters.AddRange(caseDescription.witnessNames);
        _sentenceAnalyzer.InitializeAnalysis();

        await llmCharacter.llm.WaitUntilReady();

        if (enableTTS && ttsManager != null)
            await InitializeTTSSystem();

        _courtRecordUI.isGameplay = true;
        _finalRoundStepOneDone = false;
        _finalRoundSummary = null;

        OnNextButtonClick();
    }

    private async System.Threading.Tasks.Task InitializeTTSSystem()
    {
        if (ttsManager == null || !enableTTS)
        {
            Debug.Log("TTS disabled or manager not found");
            return;
        }

        Debug.Log("Waiting for TTS to be ready...");

        int timeout = 0;
        while (!ttsManager.isInitialized && timeout < 100)
        {
            await System.Threading.Tasks.Task.Delay(100);
            timeout++;
        }

        if (!ttsManager.isInitialized)
        {
            Debug.LogError("TTS initialization timeout");
            return;
        }

        if (characterAnimator == null)
        {
            Debug.LogError("CharacterAnimator not found");
            return;
        }

        string prosecutorGender = characterAnimator.ProsecutorGender;

        Debug.Log($"Initializing TTS - Prosecutor Gender: {prosecutorGender}");

        if (_translatedDescription.witnessNames == null || _translatedDescription.witnessGenders == null)
        {
            Debug.LogError("Invalid witness data for TTS initialization");
            return;
        }

        ttsManager.InitializeCharacterVoices(_translatedDescription.witnessNames,
                                           _translatedDescription.witnessGenders,
                                           prosecutorGender);

        Debug.Log("TTS system initialized with character voices");
    }

    private void InitializeChat()
    {
        string variabilityPrompt = "";
        if (CaseMemory.NewAISeed.HasValue)
        {
            variabilityPrompt = $"\n\nSession Seed: {CaseMemory.NewAISeed.Value}. Use this seed to introduce natural variation in character behaviors, dialogue patterns, and decision-making while maintaining character consistency.";
        }
        llmCharacter.prompt = mainPrompt + variabilityPrompt;
        llmCharacter.ClearChat();
        llmCharacter.AddMessage("Case Description", BuildCaseDescriptionPrompt());
        enabled = true;
    }

    string BuildCaseDescriptionPrompt()
    {
        return $"The following is the case file for today's simulation, provided in JSON format. Use this only as factual reference during the trial. Do not repeat or explain it:\n{_caseDescription.GetJsonDescription()}";
    }

    //TODO rimuovi l'analisi del testo del giudice per garantire interventi aggiuntivi e utilizzare il formato: [number of question]
    //fixare anche il salvataggio
    //Il passaggio di parola è ancora un po' rotto: da fixare
    //Generare altri casi e testare 
    //Modificare il prompt per far adattare il giudice alla situazione
    //Aggiungere la possibilità di cambiare seed o cambiarlo randomicamente
    //Il prosecutor non deve essere cordiale
    private void InitializeRounds()
    {
        _roundsTimeline = new List<(string role, string systemMessage)>
        {
            (" "," "),
            (judgeName, $"Now the {judgeName} introduces the court case then passes the word to {attackName}"),
            (attackName,$"Now the {attackName} introduces their case thesis then asks {judgeName} the amount of questions they want to deliver to the witnesses"),
            (judgeName, $"Now the {judgeName} grants a specific number of questions to {attackName} based on the previous spoken line."),
            //(judgeName, $"Now the {judgeName} grants a specific number of questions to {attackName} based on the previous spoken line then passes the word to {defenseName}"),
            //(judgeName, $"Now the {judgeName} passes the word to {defenseName}"),
            (defenseName,$"Now the {defenseName} introduces their case thesis then asks {judgeName} the amount of questions they want to deliver to the witnesses"),
            (judgeName, $"Now the {judgeName} grants a specific number of questions to {defenseName} based on the previous spoken line."),

        };

    }


    public void SetAIText(string text)
    {
        string cleanText = text.Split(_questionCharacter)[0]
                              .Split(_gameOverCharacter)[0]
                              .Split(_requestCharacter)[0]
                              .Split(_interventionGrantCharacter)[0];

        aiText.text = cleanText;

        if (enableTTS && ttsManager != null && !string.IsNullOrWhiteSpace(cleanText))
        {
            string currentCharacter = _roundsTimeline[_round].role;

            // Skip TTS for Defense (player)
            if (!currentCharacter.ToLower().Contains(defenseName.ToLower()))
            {
                string ttsCharacterName = MapToTTSCharacter(currentCharacter);

                // Aggiorna il testo nel streaming TTS
                ttsManager.UpdateStreamingText(ttsCharacterName, cleanText);

                Debug.Log($"Updated TTS streaming for: {ttsCharacterName}");
            }
        }
    }

    private void StartTTSForCurrentCharacter()
    {
        if (!enableTTS || ttsManager == null) return;

        string currentCharacter = _roundsTimeline[_round].role;

        if (!currentCharacter.ToLower().Contains(defenseName.ToLower()))
        {
            string ttsCharacterName = MapToTTSCharacter(currentCharacter);
            ttsManager.StartStreamingTTS(ttsCharacterName);
            Debug.Log($"Started TTS streaming for: {ttsCharacterName}");
        }
    }

    private void OnTTSComplete()
    {
        nextButton.interactable = true;
        Debug.Log("TTS completed, next button re-enabled");
    }

    private string MapToTTSCharacter(string characterName)
    {
        if (characterName.ToLower().Contains(judgeName.ToLower()))
            return "Judge";
        else if (characterName.ToLower().Contains(attackName.ToLower()))
            return "Prosecutor";
        else
            return characterName;
    }
    public void AIReplyComplete()
    {
        AddToLog(aiTitle.text, aiText.text);

        // Finalizza il TTS streaming
        if (enableTTS && ttsManager != null)
        {
            string currentCharacter = _roundsTimeline[_round].role;

            if (!currentCharacter.ToLower().Contains(defenseName.ToLower()))
            {
                string ttsCharacterName = MapToTTSCharacter(currentCharacter);

                if (waitForTTSCompletion)
                {
                    nextButton.interactable = false;
                    ttsManager.FinalizeStreamingTTS(ttsCharacterName, OnTTSComplete);
                }
                else
                {
                    ttsManager.FinalizeStreamingTTS(ttsCharacterName);
                }

                Debug.Log($"Finalized TTS streaming for: {ttsCharacterName}");
            }
        }
    }
    private void OnInputFieldSubmit(string message)
    {
        OnNextButtonClick();
    }

    private async void OnNextButtonClick()
    {
        if (_pendingEndGameMessage.HasValue)
        {
            endGameUI.Show(_pendingEndGameMessage.Value.message, _pendingEndGameMessage.Value.color);

            _pendingEndGameMessage = null;

            nextButton.interactable = false;
            return;
        }

        // Stop TTS quando si avanza al prossimo turno
        if (ttsManager != null)
        {
            ttsManager.StopAllSpeech();
        }


        if (_roundsTimeline[_round].role.ToLower().Contains(defenseName.ToLower()))
        {
            if (EventSystem.current.currentSelectedGameObject == playerText.gameObject && string.IsNullOrWhiteSpace(playerText.text))
                return;
            string message = playerText.text;
            Debug.Log("Message: " + message);
            llmCharacter.AddPlayerMessage(message);
            playerText.interactable = false;
            characterAnimator.HideCurrentCharacter();
            AddToLog(defenseName, message);
            nextButton.interactable = false;
            await SetUpNextRound(message);
            EventSystem.current.SetSelectedGameObject(null);
        }

        nextButton.interactable = false;
        _ = NextRound();
    }

    private async Task NextRound(bool increment = true)
    {
        playerText.text = "...";
        aiText.text = "...";

        if (_lastPhase && _round >= _roundsTimeline.Count - 1)
        {
            characterAnimator.ShowCharacter(judgeName, "");  // Entra con animazione e poi mostra testo

            if (enableTTS && ttsManager != null)
            {
                ttsManager.StartStreamingTTS("Judge");
            }

            string verdict = await _sentenceAnalyzer.FinalVerdict(_caseDescription, llmCharacter.chat, _translatedDescription.language, SetAIText, AIReplyComplete);

            if (verdict.Contains(_winTag))
                _pendingEndGameMessage = ("HAI VINTO", Color.green);
            else
                _pendingEndGameMessage = ("HAI PERSO", Color.red);

            nextButton.interactable = true;

            return;
        }

        if (increment) _round++;

        if (!_lastPhase) CheckForJudgeIntervention();

        systemMessages.text = _roundsTimeline[_round].systemMessage;
        characterAnimator.ShowCharacter(_roundsTimeline[_round].role, "");  // Entra con animazione e poi mostra testo


        if (_roundsTimeline[_round].role.ToLower().Contains(defenseName.ToLower()))
        {

            playerText.interactable = true;
            playerText.gameObject.SetActive(true);
            playerText.text = "";
            playerText.Select();

            micInput.EnableMicInput(true);
        }
        else
        {
            playerText.interactable = false;
            playerText.gameObject.SetActive(false);
            micInput.EnableMicInput(false);

            aiTitle.text = _roundsTimeline[_round].role;
            string systemMessage = _roundsTimeline[_round].systemMessage;
            if (systemMessage != "")
                llmCharacter.AddSystemMessage(systemMessage);

            StartTTSForCurrentCharacter();

            string answer = await llmCharacter.ContinueChat(_roundsTimeline[_round].role, SetAIText, AIReplyComplete);

            if (!_lastPhase) await SetUpNextRound(answer);

        }

        nextButton.interactable = true;

    }

    async Task SetUpNextRound(string text)
    {

        //TODO remove the next character name from the main prompt of The Court object then test the new analyse prompt

        var characters = new List<string> { judgeName, defenseName, attackName };
        string[] data = new string[2];
        characters.AddRange(_caseDescription.witnessNames);

        if (_roundsTimeline[_round].role.ToLower().Contains(defenseName.ToLower()))
        {

            var nextCharTask = _sentenceAnalyzer.AnalyzeNextCharacter(llmCharacter.chat, characters.ToArray());
            if (enableAnalyzeInfo)
            {
                var infoReqTask = _sentenceAnalyzer.AnalyzeInfoNeeded(llmCharacter.chat, _caseDescription);
                data = await Task.WhenAll(nextCharTask, infoReqTask);
            }
            else
                data[0] = await nextCharTask;
        }
        else
        {

            if (_roundsTimeline[_round].role.ToLower().Contains(judgeName.ToLower()) && _round > 0)
            {
                Match grantMatch = Regex.Match(text, @"\[(.*?)\]");
                string prevCharacter = _roundsTimeline[_round - 1].role;
                if (!(grantMatch.Success && int.TryParse(grantMatch.Groups[1].Value, out var grantNum)))
                    (grantNum, _) = await _sentenceAnalyzer.AnalyzeGrantInterventions(llmCharacter.chat, new[] { attackName, defenseName });
                if (!prevCharacter.Contains("NULL") && grantNum > 0)
                    if (prevCharacter.ToLower().Contains(defenseName.ToLower()))
                    {
                        Debug.Log("incremented defense");
                        _defenseInteractions += grantNum;
                    }
                    else if (prevCharacter.ToLower().Contains(attackName.ToLower()))
                    {
                        Debug.Log("incremented attack");
                        _attackInteractions += grantNum;
                    }

            }
            else if (enableAnalyzeInfo && _roundsTimeline[_round].role.ToLower().Contains(attackName.ToLower()))
                data[1] = await _sentenceAnalyzer.AnalyzeInfoNeeded(llmCharacter.chat, _caseDescription);
            //else 
            //    data[1] = Regex.Match(text, @"\*(.*?)\*").Groups[1].Value;

            Debug.Log(_roundsTimeline[_round].role + ": " + text);
            data[0] = Regex.Match(text, @"<([^>]+)>").Groups[1].Value;

        }

        //TODO fix the additional information requests and the logic of choosing the next character

        if (data[0].ToLower().Contains("null"))
            data[0] = _caseDescription.witnessNames[Random.Range(0, _caseDescription.witnessNames.Count)];

        if (_roundsTimeline.Count <= _round + 1)
            _roundsTimeline.Insert(_round + 1, (data[0], ""));
        else if (_roundsTimeline[_round + 1] == ("", ""))
            _roundsTimeline[_round + 1] = (data[0], "");

        if (!string.IsNullOrWhiteSpace(data[1]) && !data[1].ToUpper().Contains("NULL"))
            try
            {
                (string answer, string translatedAnswer) = await _apiManager.RequestAdditionalInfo(_caseDescription.GetTotalDescription(new[] { 0, 2, 3, 4 }), data[1], _translatedDescription.language);
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    _caseDescription.AddInformation(answer);
                    _translatedDescription.AddInformation(translatedAnswer);
                }

                llmCharacter.chat[1] = new ChatMessage { role = "Case Description", content = BuildCaseDescriptionPrompt() };
                caseDescriptionText.text = _translatedDescription.GetTotalDescription(true);


            }
            catch (Exception e)
            {
                Debug.LogWarning("Additional Information API call Failed: " + e.Message);
            }
    }

    //TODO rendere la tesi finale del Prosecutor più coerente con quanto successo in aula; il Prosecutor dovrebbe fare domande distribuite tra i vari testimoni
    void CheckForJudgeIntervention()
    {

        Debug.Log("Attack interactions:" + _attackInteractions + "\nDefense interactions:" + _defenseInteractions);


        string role = _roundsTimeline[_round].role;
        if (role.ToLower().Contains(attackName.ToLower()))
            if (_attackInteractions <= 0)
                _roundsTimeline[_round] = (judgeName, $"Last message was addressed to {attackName} but they are out of interventions; the Judge must take care of the situation");
            else
            {
                if (string.IsNullOrWhiteSpace(_roundsTimeline[_round].systemMessage))
                    _roundsTimeline[_round] = (attackName, _attackInteractions + " interventions remaining for " + attackName);
                _attackInteractions--;
            }
        else if (role.ToLower().Contains(defenseName.ToLower()))
            if (_defenseInteractions <= 0)
                _roundsTimeline[_round] = (judgeName, $"Last message was addressed to {defenseName} but they are out of interventions; the Judge must take care of the situation");
            else
            {
                if (string.IsNullOrWhiteSpace(_roundsTimeline[_round].systemMessage))
                    _roundsTimeline[_round] = (defenseName, _defenseInteractions + " interventions remaining for " + defenseName);
                _defenseInteractions--;
            }

        if (IsOutOfQuestions && _roundsTimeline[_round].role.ToLower().Contains(judgeName.ToLower()))
        {
            _roundsTimeline[_round] = (judgeName, $"Now the {judgeName} requests the final thesis of the {attackName} and {defenseName}");
            _roundsTimeline.Add((attackName, $"Now the {attackName} shows their final thesis based on what happened in the courtroom"));
            _roundsTimeline.Add((defenseName, $"Now the {defenseName} shows their final thesis"));
            _roundsTimeline.Add((judgeName, $"Now the {judgeName} announces to everyone they are going to issue the final verdict"));

            _lastPhase = true;
        }

    }

    void AddToLog(string role, string content, string roleColor = "#550505")
    {
        logText.text += $"<b><color={roleColor}>{role}</color></b>: {content}\n\n";
    }

    /*public void SetCurrentRound(int round)
    {
        if (round >= 0 && round < _roundsTimeline.Count)
        {
            _round = round;
            
            systemMessages.text = _roundsTimeline[_round].systemMessage;

        }
       
    }*/

    //Property Getter per il retry e per il save
    public CaseDescription GetCaseDescription() => _caseDescription;
    public CaseDescription GetTranslatedDescription() => _translatedDescription;
    //public int GetCurrentRound() => _round;

}



public class CaseDescription
{
    public string title;
    public string playerGoal;
    public string summary;
    public string shortSummary;
    public List<string> evidence;
    public List<string> witnessNames;
    public List<string> witnessDescriptions;
    public List<string> witnessGenders;
    public List<string> additionalInfo;
    public string language;


    /// <summary> Indexes of section names:
    /// 0 - Player Goal |
    /// 1 - Case Name |
    /// 2 - Long Summary |
    /// 3 - Short Summary |
    /// 4 - Evidence |
    /// 5 - Witnesses |
    /// 6 - Additional info 
    /// </summary>
    private List<string> sectionNames;
    private List<string> fallbackSectionNames;


    private string[] descArray;
    private string[] richArray;
    private int fileID;


    [JsonConstructor]
    public CaseDescription(string title, string playerGoal, string summary, string shortSummary, List<string> evidence, List<string> witnessNames, List<string> witnessDescriptions, List<string> sectionNames, List<string> witnessGenders = null)
    {
        this.title = title;
        this.playerGoal = playerGoal;
        this.summary = summary;
        this.shortSummary = shortSummary;
        this.evidence = evidence.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        this.witnessNames = witnessNames.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        this.witnessDescriptions = witnessDescriptions.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        fallbackSectionNames = new List<string>() { "Title", "Player Goal", "Summary", "Short Summary", "Evidence", "Witnesses", "Additional Info" };
        if (sectionNames == null)
            sectionNames = fallbackSectionNames;
        else
            sectionNames.AddRange(fallbackSectionNames.TakeLast(fallbackSectionNames.Count - sectionNames.Count));

        this.sectionNames = sectionNames;

        // Gestione retrocompatibilità utile solo in debug per casi precedenti dove non erano presenti i generi dei testimoni
        if (witnessGenders == null || witnessGenders.Count != witnessNames.Count)
        {
            this.witnessGenders = witnessNames.Select(name => UnityEngine.Random.Range(0, 2) == 0 ? "M" : "F").ToList();
            Debug.Log("Case caricato senza gender info - assegnati generi casuali");
        }
        else
        {

            this.witnessGenders = witnessGenders.Select(g => g.Trim().ToUpper() == "M" ? "M" : "F").ToList();
        }


        descArray = new string[6];
        richArray = new string[6];
        additionalInfo = new List<string>();
        fileID = -1;
        UpdateCaseDescription();
    }

    //TODO Additional info is not included into Json Case Description, find a way to add that. Find a way to Remove the Build Prompt method.
    public string GetJsonDescription()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    public string GetBriefDescription(bool rich = false)
    {

        if (rich)
            return richArray[0] +
                   $"<b><color=#F64A3E>{GetSectionName(2)}</color></b>\n" +
                   shortSummary;

        return descArray[0] +
               $"{GetSectionName(2)}\n" +
               shortSummary;

    }

    public string GetTotalDescription(bool rich = false, bool update = true)
    {
        if (update) UpdateCaseDescription();
        return string.Join("", rich ? richArray : descArray);
    }
    public string GetTotalDescription(int index, bool rich = false, bool update = true)
    {
        if (update) UpdateCaseDescription();
        string[] currentDescArr = rich ? richArray : descArray;
        return currentDescArr[Mathf.Clamp(index, 0, descArray.Length - 1)];
    }
    public string GetTotalDescription(int[] indexes, bool rich = false, bool update = true)
    {
        if (update) UpdateCaseDescription();
        string[] currentDescArr = rich ? richArray : descArray;
        return string.Join("", indexes.Select(x => currentDescArr[Mathf.Clamp(x, 0, currentDescArr.Length - 1)]).ToArray());
    }

    private void UpdateCaseDescription()
    {
        var list = witnessNames;
        var descriptions = witnessDescriptions;

        descArray[0] = $"{GetSectionName(0)}\n" +
                       $"{title}\n\n";
        descArray[1] = $"{GetSectionName(1)}\n" +
                       $"{playerGoal}\n\n";
        descArray[2] = $"{GetSectionName(2)}\n" +
                       $"{summary}\n\n";
        descArray[3] = $"{GetSectionName(4)}\n" +
                       $"{string.Join("\n", evidence.Select(x => "-" + x))}\n\n";
        descArray[4] = $"{GetSectionName(5)}\n" +
                       string.Join("\n", witnessNames.Select(x => $"-{x}: {descriptions[list.IndexOf(x)]}").ToArray());
        //$"{string.Join("\n", witnesses.Select(x => $"-{x.name}: {x.description}\n{x.personality}\n{x.testimony}").ToArray())}\n\n";
        descArray[5] = $"\n\n{GetSectionName(6)}\n" +
                       $"{string.Join("\n", additionalInfo.Select(x => "-" + x.Replace("\n", "")))}\n\n";

        richArray[0] = $"<b><color=#F64A3E>{GetSectionName(0)}</color></b>\n" +
                       $"{title}\n\n";
        richArray[1] = $"<b><color=#F64A3E>{GetSectionName(1)}</color></b>\n" +
                       $"{playerGoal}\n\n";
        richArray[2] = $"<b><color=#F64A3E>{GetSectionName(2)}</color></b>\n" +
                       $"{summary}\n\n";
        richArray[3] = $"<b><color=#F64A3E>{GetSectionName(4)}</color></b>\n" +
                       $"{string.Join("\n", evidence.Select(x => "-" + x))}\n\n";
        richArray[4] = $"<b><color=#F64A3E>{GetSectionName(5)}</color></b>\n" +
                       string.Join("\n", witnessNames.Select(x => $"-<i><color=#550505>{x}</color></i>: {descriptions[list.IndexOf(x)]}").ToArray());
        //$"{string.Join("\n", witnesses.Select(x => $"-<i><color=#550505>{x.name}</color></i>: {x.description}\n{x.personality}\n{x.testimony}").ToArray())}\n\n";
        richArray[5] = $"\n\n<b><color=#F64A3E>{GetSectionName(6)}</color></b>\n" +
                       $"{string.Join("\n", additionalInfo.Select(x => "- " + x.Replace("\n", "")))}\n\n";
    }

    private string GetSectionName(int index)
    {
        if (string.IsNullOrWhiteSpace(sectionNames[index]))
            sectionNames[index] = fallbackSectionNames[index];

        return sectionNames[index];
    }

    public void AddInformation(string info)
    {
        additionalInfo.Add(info);
    }

    public void SetID(int ID)
    {
        fileID = ID;
    }

    public int GetID()
    {
        return fileID;
    }

    public bool IsSaved()
    {
        return fileID > -1;
    }

}

//public struct Witness
//{
//    public string name;
//    public string description;
//    public string personality;
//    public string testimony;
//
//    public Witness(string name, string description, string personality, string testimony)
//    {
//        this.name = name;
//        this.description = description;
//        this.personality = personality;
//        this.testimony = testimony;
//    }
//}