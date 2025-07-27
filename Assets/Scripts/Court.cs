using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLMUnity;
using NUnit.Framework;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;
using UnityEngine.UI;
using NotImplementedException = System.NotImplementedException;

public class Court : MonoBehaviour
{
    //References
    [SerializeField] private LLMCharacter llmCharacter;
    [SerializeField] private InputField playerText;
    [SerializeField] private TextMeshProUGUI aiText;
    [SerializeField] private Text aiTitle;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI systemMessages;
    [SerializeField] private TextMeshProUGUI caseDescriptionText;
    [SerializeField] private Button nextButton;
    private APIInterface _apiManager;
    private SentenceAnalyzer _sentenceAnalyzer;
    [SerializeField] RunJets runJets;
    [SerializeField] public Button micButton;
    public MicrophoneInput micInput;
    [SerializeField] private CharacterAnimator characterAnimator;
    [SerializeField] private EndGameUI endGameUI;
    [SerializeField] private TextMeshProUGUI playerGoalText;
    private CourtRecordUI _courtRecordUI;

    //Names
    [SerializeField] private string wildcardCharacterName = "Wildcard";
    [SerializeField] private string defenseName = "Defense";
    [SerializeField] private string attackName = "Prosecutor";
    [SerializeField] private string judgeName = "Judge";

    //Prompts
    [TextArea(5, 10)] public string mainPrompt = "A court case where the AI takes control of several characters listed below";
    [TextArea(5, 10)] public string wildcardCharacterPrompt = "Whenever you encounter the Wildcard name you need to take control of the next character in the dialogue who fit the best; in addition, you must specify the character's name at the beginning of the sentence in this manner: Name of character>";
    [TextArea(5, 10)] public string judgePrompt = "The goal of Judge is to give the defendant's final sentence by listening to the dialogue";
    [TextArea(5, 10)] public string attackPrompt = "The goal of Prosecutor is proving to the Judge that the defendant is guilty";

    //Command text
    private readonly string _questionCharacter = ">";
    private readonly string _requestCharacter = "*";
    private readonly string _gameOverCharacter = "#";

    //Gameplay
    [SerializeField, UnityEngine.Range(1, 5)] private int numOfQuestions;
    public bool PlayerCanAct => _roundsTimeline[_round].role == defenseName;

    //State track
    private List<(string role, string systemMessage)> _roundsTimeline;
    private CaseDescription _caseDescription, _translatedDescription;
    private int _round;

    //End game message
    private List<string> winKeywords = new List<string> { "non colpevole", "innocente", "assolto" };
    private List<string> loseKeywords = new List<string> { "colpevole", "condannato", "condanno" };
    private (string message, Color color)? _pendingEndGameMessage = null;

    //Events
    public event Action<bool> GameOverCallback;


    private void Awake()
    {
        _apiManager = FindFirstObjectByType<APIInterface>();
        _sentenceAnalyzer = FindFirstObjectByType<SentenceAnalyzer>();

        playerText.onSubmit.AddListener(OnInputFieldSubmit);
        nextButton.onClick.AddListener(OnNextButtonClick);

        llmCharacter.playerName = defenseName;

        _courtRecordUI = FindFirstObjectByType<CourtRecordUI>();
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //async void Start()
    //{
    //    playerText.interactable = false;
    //    playerText.gameObject.SetActive(false);
    //    micButton.interactable = false;
    //    nextButton.interactable = false;
    //    
    //    while (string.IsNullOrWhiteSpace(((_caseDescription, _translatedDescription) = await _apiManager.Request())._caseDescription.summary)) {Debug.LogWarning("Empty case description");};
    //
    //    characterAnimator.AssignDynamicPrefabs(_caseDescription.witnesses.Keys.ToList(), attackName);
    //
    //    InitializeChat();
    //    InitializeRounds();
    //      
    //    caseDescriptionText.text = _translatedDescription.GetTotalDescription(true);
    //    
    //    
    //    await llmCharacter.llmCharacter.WaitUntilReady();
    //    nextButton.interactable = true;
    //    
    //
    //    
    //}

    private void Update()
    {
        if (nextButton.interactable && Input.GetKeyDown(KeyCode.Return))
        {
            OnNextButtonClick();
        }
    }


    public async void InitializeCourt(CaseDescription caseDescription, CaseDescription translatedDescription)
    {
        _caseDescription = caseDescription;
        _translatedDescription = translatedDescription;

        if (playerGoalText != null)
            playerGoalText.text = $"<b><color=#F64A3E>{_translatedDescription.sectionTitles[0]}</color></b>\n{_translatedDescription.playerGoal}";


        InitializeChat();
        InitializeRounds();

        caseDescriptionText.text = _translatedDescription.GetTotalDescription(true);
        characterAnimator.AssignDynamicPrefabs(_caseDescription.witnesses.Keys.ToList(), attackName);

        //string[] characters = new string[] { judgeName, defenseName, attackName };
        //characters.AddRange(caseDescription.witnesses.Keys.ToList());
        //_sentenceAnalyzer.InitializeAnalysis(characters);

        await llmCharacter.llm.WaitUntilReady();

        _courtRecordUI.isGameplay = true;

        OnNextButtonClick();
    }

    private void InitializeChat()
    {
        //llmCharacter.prompt = $"{mainPrompt}\n{judgeName} - {judgePrompt}\n{attackName} - {attackPrompt}\n\n";
        //llmCharacter.prompt += _caseDescription.GetTotalDescription(3) + _caseDescription.GetTotalDescription(new int[]{0,1,2});
        //foreach (var item in _caseDescription.witnesses)
        //    llmCharacter.prompt += $"{item.Key} - {item.Value}\n";

        //llmCharacter.prompt += $"\n {APIInterface.RemoveSplitters(string.Join("",_caseDescription.totalDescription.Split(APIInterface.sectionSplitCharacters).Take(3).ToArray()))}";

        llmCharacter.prompt = BuildPrompt();
        Debug.Log(llmCharacter.gameObject.name + llmCharacter.prompt);
        llmCharacter.ClearChat();
    }

    string BuildPrompt()
    {
        return
            $"{mainPrompt}\n{wildcardCharacterName} - {wildcardCharacterPrompt}\n{judgeName} - {judgePrompt}\n{attackName} - {attackPrompt}\n\n" +
            _caseDescription.GetTotalDescription(new int[] { 4, 0, 1, 2, 3, 5 });
    }

    private void InitializeRounds()
    {
        _roundsTimeline = new List<(string role, string systemMessage)>
        {
            (" "," "),
            (judgeName, $"Introduction Round: Now the {judgeName} introduces the court case then passes the word to {attackName}"),
            //(attackName, $"Introduction Round: Now the {attackName} exposes the clues trying to convince the judge"),
            (attackName, $"Introduction Round: Now the {attackName} tries to convince the judge about the defendant's guilt"),
            (defenseName, $"Introduction Round: Now the {defenseName} dismantle the evidence trying to convince the judge")
        };

        for (int i = 0; i < numOfQuestions; i++)
        {
            _roundsTimeline.Add((attackName, $"Interrogation Round: {attackName} should interrogate the witnesses  - Questions remaining: " + (numOfQuestions - i)));
            _roundsTimeline.Add((wildcardCharacterName, $"Interrogation Round: the AI must take control of a character"));
        }

        for (int i = 0; i < numOfQuestions; i++)
        {
            _roundsTimeline.Add((defenseName, $"Interrogation Round: {defenseName} should interrogate the witnesses - Questions remaining: " + (numOfQuestions - i)));
            _roundsTimeline.Add((wildcardCharacterName, $"Interrogation Round: the AI must take control of a character"));
        }

        _roundsTimeline.Add((attackName, $"Final Round: Now it's the {attackName} last intervention"));
        _roundsTimeline.Add((defenseName, $"Final Round: Now it's the {defenseName} last intervention"));
        _roundsTimeline.Add((judgeName, $"Final Round: Now the {judgeName} give their final sentence"));
    }

    private async void OnInputFieldSubmit(string message)
    {
        playerText.interactable = false;
        aiText.text = "...";
        llmCharacter.AddPlayerMessage(message);
        await CheckSpecialCharacters(message);
        characterAnimator.HideCurrentCharacter();
        logText.text += $"<b><color=#550505>{defenseName}</color></b>: {message}\n\n";

        nextButton.interactable = false;
        await NextRound();
    }

    public void SetAIText(string text)
    {

        if (_roundsTimeline[_round].role == wildcardCharacterName)
        {
            if (text.Contains(_questionCharacter))
            {
                aiTitle.text = text.Split(_questionCharacter)[0];
                aiText.text = text.Split(_questionCharacter)[1].Split(_requestCharacter)[0];
            }

            return;
        }

        aiText.text = text.Split(_requestCharacter)[0];

    }

    public void AIReplyComplete()
    {
        logText.text += $"<b><color=#550505>{aiTitle.text}</color></b>: {aiText.text}\n\n";
        //nextButton.interactable = true;
        //runJets.TextToSpeech(aiText.text);
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

        //forzo il player a scivere qualcosa
        if (_roundsTimeline[_round].role == defenseName)
        {
            if (EventSystem.current.currentSelectedGameObject == playerText.gameObject &&
                string.IsNullOrWhiteSpace(playerText.text))
            {
                return;
            }
            OnInputFieldSubmit(playerText.text);
            EventSystem.current.SetSelectedGameObject(null);
        }
        else
        {
            nextButton.interactable = false;
            await NextRound();
        }
    }




    private async Task NextRound(bool increment = true)
    {
        if (increment) _round++;
        systemMessages.text = _roundsTimeline[_round].systemMessage;

        if (_roundsTimeline[_round].role == defenseName)
        {
            characterAnimator.ShowCharacter(defenseName, ""); // Entra il player

            playerText.interactable = true;
            playerText.gameObject.SetActive(true);
            playerText.text = "";
            playerText.Select();

            micInput.EnableMicInput(true);
            nextButton.interactable = true;
        }
        else
        {
            playerText.interactable = false;
            playerText.gameObject.SetActive(false);
            micInput.EnableMicInput(false);

            if (_roundsTimeline[_round].role != wildcardCharacterName)
                aiTitle.text = _roundsTimeline[_round].role;
            else
                aiTitle.text = "";

            string systemMessage = _roundsTimeline[_round].systemMessage;
            if (systemMessage != "")
                llmCharacter.AddSystemMessage(systemMessage);

            characterAnimator.ShowCharacter(_roundsTimeline[_round].role, "");  // Entra con animazione e poi mostra testo

            string answer = await llmCharacter.ContinueChat(_roundsTimeline[_round].role, SetAIText, AIReplyComplete);

            await CheckSpecialCharacters(answer);

            // Se e' l'ultimo round (il giudice emette il verdetto)
            if (_round == _roundsTimeline.Count - 1)
            {
                string infoRequest = answer.Split(_gameOverCharacter)[1].Replace("\n", "");
                string verdict = answer.ToLower();

                if (winKeywords.Any(k => verdict.Contains(k)) || infoRequest.Contains("VITTORIA"))
                {
                    _pendingEndGameMessage = ("HAI VINTO", Color.green);
                }
                else if (loseKeywords.Any(k => verdict.Contains(k)) || infoRequest.Contains("SCONFITTA"))
                {
                    _pendingEndGameMessage = ("HAI PERSO", Color.red);

                    //Salvo il caso per retry
                    //endGameUI.SaveCase(_caseDescription.GetTotalDescription(false), _translatedDescription.GetTotalDescription(false));
                }
            }


            nextButton.interactable = true;
        }

    }

    private async Task CheckSpecialCharacters(string text)
    {

        //logText.text += $"<b><color=#550505>{_roundsTimeline[_round].role}</color></b>: {text.Split(_questionCharacter)[0]}\n\n";

        //if(text.Contains(_questionCharacter))
        //    try
        //    {
        //        string questionedWitness = text.Split(_questionCharacter)[1].Replace(" ", "").Replace("\n", "");
        //        _roundsTimeline.Insert(_round + 1, (questionedWitness , ""));
        //    }
        //    catch (IndexOutOfRangeException e)
        //    {
        //        Debug.LogWarning(e.Message + $"\nSplitting by {_questionCharacter} failed");
        //    }

        if (text.Contains(_requestCharacter))
        {
            try
            {
                string infoRequest = text.Split(_requestCharacter)[1].Replace("\n", "");
                if (!string.IsNullOrWhiteSpace(infoRequest))
                {
                    string answer, translatedAnswer;
                    (answer, translatedAnswer) = await _apiManager.Request(_caseDescription.GetTotalDescription(), infoRequest);
                    if (!string.IsNullOrWhiteSpace(answer))
                    {
                        _caseDescription.additionalInfo.Add(answer);
                        _translatedDescription.additionalInfo.Add(translatedAnswer);

                        llmCharacter.chat[0] = new ChatMessage() { role = "system", content = BuildPrompt() };
                        caseDescriptionText.text = _translatedDescription.GetTotalDescription(true);
                    }

                }

            }
            catch (IndexOutOfRangeException e)
            {
                Debug.LogWarning(e.Message + $"\nSplitting by {_requestCharacter} failed");
            }

        }

        if (text.Contains(_gameOverCharacter))
        {
            try
            {
                string infoRequest = text.Split(_gameOverCharacter)[1].Replace("\n", "");
                GameOverCallback?.Invoke(infoRequest.Contains("VITTORIA"));
            }
            catch (IndexOutOfRangeException e)
            {
                Debug.LogWarning(e.Message + $"\nSplitting by {_gameOverCharacter} failed");
            }
        }



    }

    private void UpdateDescriptions()
    {

    }

    private string RemoveCommandText(string text)
    {
        return text.Split(_questionCharacter)[0].Split(_requestCharacter)[0];
    }

    private async Task RequestAndUpdateDescriptions(string request)
    {

        string text, translated;
        (text, translated) = await _apiManager.Request(_caseDescription.GetTotalDescription(), request);
        _caseDescription.clues.Add(text);
        _translatedDescription.clues.Add(translated);

    }

    //Property Getter per il retry 
    public CaseDescription GetCaseDescription() => _caseDescription;
    public CaseDescription GetTranslatedDescription() => _translatedDescription;

}

public struct CaseDescription
{
    public string playerGoal;
    public string title;
    public string summary;
    public string shortSummary;
    public List<string> clues;
    public Dictionary<string, string> witnesses;
    public List<string> additionalInfo;
    private string[] descArray;
    private string[] richArray;

    /// <summary> Indexes of section titles:
    /// 0 - Player Goal |
    /// 1 - Case Name
    /// 2 - Long Summary |
    /// 3 - Short Summary |
    /// 4 - Evidence |
    /// 5 - Witnesses |
    /// 6 - Additional info 
    /// </summary>
    public string[] sectionTitles;

    public CaseDescription(string description, string sectionSplitCharacters, string subsectionSplitCharacters)
    {
        string[] answer = description.Split(sectionSplitCharacters, StringSplitOptions.RemoveEmptyEntries);
        Dictionary<string, string> witnessDictionary = new Dictionary<string, string>();
        string[] witnessesArr = answer[5].Split(">")[1].Split(subsectionSplitCharacters, StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in witnessesArr)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string[] itemSplit = item.Split(":");
            witnessDictionary.Add(itemSplit[0].Replace("-", "").Replace("\n", ""), itemSplit[1].Replace("\n", ""));
        }

        playerGoal = answer[0].Split(">")[1].Replace("\n", "");
        title = answer[1].Split(">")[1].Replace("\n", "");
        summary = answer[2].Split(">")[1].Replace("\n", "");
        shortSummary = answer[3].Split(">")[1].Replace("\n", "");
        clues = answer[4].Split(">")[1].Split(subsectionSplitCharacters, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Replace("-", "").Replace("\n", "")).ToList();
        witnesses = witnessDictionary;
        sectionTitles = answer.Select(x => x.Split(">")[0].Replace("\n", "")).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        additionalInfo = new List<string>();

        descArray = new string[sectionTitles.Length - 1];
        richArray = new string[sectionTitles.Length - 1];

        UpdateCaseDescription();
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

    public string GetBriefDescription(bool rich = false)
    {
        if (rich)
            return richArray[1] +
                   $"<b><color=#F64A3E>{sectionTitles[2]}</color></b>\n" +
                   shortSummary;
        else
            return descArray[1] +
                   $"{sectionTitles[2]}\n" +
                   shortSummary;
    }

    private void UpdateCaseDescription()
    {
        descArray[0] = $"{sectionTitles[0]}\n" +
                       $"{playerGoal}\n\n";
        descArray[1] = $"{sectionTitles[1]}\n" +
                       $"{title}\n\n";
        descArray[2] = $"{sectionTitles[3]}\n" +
                       $"{shortSummary}\n\n";
        descArray[3] = $"{sectionTitles[4]}\n" +
                       $"{string.Join("\n", clues.Select(x => "-" + x))}\n\n";
        descArray[4] = $"{sectionTitles[5]}\n" +
                       $"{string.Join("\n", witnesses.Select(x => $"-{x.Key}: {x.Value}").ToArray())}\n\n";
        descArray[5] = $"{sectionTitles[6]}\n" +
                       $"{string.Join("\n", additionalInfo.Select(x => "-" + x))}\n\n";

        richArray[0] = $"<b><color=#F64A3E>{sectionTitles[0]}</color></b>\n" +
                       $"{playerGoal}\n\n";
        richArray[1] = $"<b><color=#F64A3E>{sectionTitles[1]}</color></b>\n" +
                       $"{title}\n\n";
        richArray[2] = $"<b><color=#F64A3E>{sectionTitles[3]}</color></b>\n" +
                       $"{shortSummary}\n\n";
        richArray[3] = $"<b><color=#F64A3E>{sectionTitles[4]}</color></b>\n" +
                       $"{string.Join("\n", clues.Select(x => "-" + x))}\n\n";
        richArray[4] = $"<b><color=#F64A3E>{sectionTitles[5]}</color></b>\n" +
                       $"{string.Join("\n", witnesses.Select(x => $"-<i><color=#550505>{x.Key}</color></i>: {x.Value}").ToArray())}\n\n";
        richArray[5] = $"<b><color=#F64A3E>{sectionTitles[6]}</color></b>\n" +
                       $"{string.Join("\n", additionalInfo.Select(x => "-" + x))}\n\n";

        //totalDescription = $"{sectionTitles[0]}\n" +
        //                                   $"{title}\n\n" +
        //                                   $"{sectionTitles[1]}\n" +
        //                                   $"{summary}\n\n" +
        //                                   $"{sectionTitles[2]}\n" +
        //                                   $"{string.Join("\n",clues.Select(x => "-" + x))}\n\n" +
        //                                   $"{sectionTitles[3]}\n" +
        //                                   $"{string.Join("\n",witnesses.Select(x =>  $"-{x.Key}: {x.Value}").ToArray())}\n\n" +
        //                                   $"{sectionTitles[4]}\n" +
        //                                   $"{string.Join("\n",additionalInfo.Select(x => "-" + x))}\n\n";
        //
        //richTextDescription = $"<b><color=#F64A3E>{sectionTitles[0]}</color></b>\n" +
        //                      $"{title}\n\n" +
        //                      $"<b><color=#F64A3E>{sectionTitles[1]}</color></b>\n" +
        //                      $"{summary}\n\n" +
        //                      $"<b><color=#F64A3E>{sectionTitles[2]}</color></b>\n" +
        //                      $"{string.Join("\n", clues.Select(x => "-" + x))}\n\n" +
        //                      $"<b><color=#F64A3E>{sectionTitles[3]}</color></b>\n" +
        //                      $"{string.Join("\n", witnesses.Select(x => $"-<i><color=#550505>{x.Key}</color></i>: {x.Value}").ToArray())}\n\n" +
        //                      $"<b><color=#F64A3E>{sectionTitles[4]}</color></b>\n" +
        //                      $"{string.Join("\n", additionalInfo.Select(x => "-" + x))}\n\n";
    }


}