﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLMUnity;
using Newtonsoft.Json;
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
    [SerializeField, UnityEngine.Range(1, 5)] private int numOfInteractions;
    private int _defenseInteractions;
    private int _attackInteractions;
    
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
    private bool _finalRoundStepOneDone = false;
    private string _finalRoundSummary = null;



    private void Awake()
    {
        _defenseInteractions = _attackInteractions = numOfInteractions;
        
        _apiManager = FindFirstObjectByType<APIInterface>();
        _sentenceAnalyzer = FindFirstObjectByType<SentenceAnalyzer>();

        playerText.onSubmit.AddListener(OnInputFieldSubmit);
        nextButton.onClick.AddListener(OnNextButtonClick);

        llmCharacter.playerName = defenseName;

        _courtRecordUI = FindFirstObjectByType<CourtRecordUI>();
        
    }
    private void Update()
    {
        if (nextButton.interactable && Input.GetKeyDown(KeyCode.Return))
            OnNextButtonClick();
    }


  
    public async void InitializeCourt(CaseDescription caseDescription, CaseDescription translatedDescription)
    {
        _caseDescription = caseDescription;
        _translatedDescription = translatedDescription;

        if (playerGoalText)
            playerGoalText.text = _translatedDescription.GetTotalDescription(1, true);


        InitializeChat();
        InitializeRounds();

        caseDescriptionText.text = _translatedDescription.GetTotalDescription(true);
        characterAnimator.AssignDynamicPrefabs(_translatedDescription.witnessNames, attackName);
        
        List<string> characters = new List<string>{ judgeName, defenseName, attackName };
        characters.AddRange(caseDescription.witnessNames);
        _sentenceAnalyzer.InitializeAnalysis(characters.ToArray());

        await llmCharacter.llm.WaitUntilReady();

        _courtRecordUI.isGameplay = true;

        _finalRoundStepOneDone = false;
        _finalRoundSummary = null;

        OnNextButtonClick();
    }


    private void InitializeChat()
    {
        llmCharacter.prompt = BuildPrompt();
        llmCharacter.ClearChat();
    }

    string BuildPrompt()
    {
        //return
        //    $"{mainPrompt}\n{wildcardCharacterName} - {wildcardCharacterPrompt}\n{judgeName} - {judgePrompt}\n{attackName} - {attackPrompt}\n\n" +
        //    _caseDescription.GetTotalDescription(new int[] { 4, 0, 1, 2, 3, 5 });
        return
            $"{mainPrompt}\n{judgeName} - {judgePrompt}\n{attackName} - {attackPrompt}\n\n" +
            _caseDescription.GetTotalDescription(new int[] { 4, 0, 1, 2, 3, 5 });
        
        
    }

    //private void InitializeRounds()
    //{
    //    _roundsTimeline = new List<(string role, string systemMessage)>
    //    {
    //        (" "," "),
    //        (judgeName, $"Introduction Round: Now the {judgeName} introduces the court case then passes the word to {attackName}"),
    //        (attackName, $"Introduction Round: Now the {attackName} tries to convince the judge about the defendant's guilt"),
    //        (defenseName, $"Introduction Round: Now the {defenseName} dismantle the evidence trying to convince the judge")
    //    };
    //
    //    for (int i = 0; i < numOfInteractions; i++)
    //    {
    //        _roundsTimeline.Add((attackName, $"Interrogation Round: {attackName} should interrogate the witnesses  - Questions remaining: " + (numOfInteractions - i)));
    //        _roundsTimeline.Add((wildcardCharacterName, $"Interrogation Round: the AI must take control of a character"));
    //    }
    //
    //    for (int i = 0; i < numOfInteractions; i++)
    //    {
    //        _roundsTimeline.Add((defenseName, $"Interrogation Round: {defenseName} should interrogate the witnesses - Questions remaining: " + (numOfInteractions - i)));
    //        _roundsTimeline.Add((wildcardCharacterName, $"Interrogation Round: the AI must take control of a character"));
    //    }
    //
    //    _roundsTimeline.Add((attackName, $"Final Round: Now it's the {attackName} last intervention"));
    //    _roundsTimeline.Add((defenseName, $"Final Round: Now it's the {defenseName} last intervention"));
    //    _roundsTimeline.Add((judgeName, $"Final Round: Now the {judgeName} give their final sentence"));
    //}
    private void InitializeRounds()
    {
        _roundsTimeline = new List<(string role, string systemMessage)>
        {
            (" "," "),
            (judgeName, $"Now the {judgeName} introduces the court case then passes the word to {attackName}"),
            (judgeName, $"Now the {judgeName} give their final verdict")
        };

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
        AddToLog(aiTitle.text, aiText.text);
        //runJets.TextToSpeech(aiText.text);
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

        if (_roundsTimeline[_round].role.ToLower().Contains(defenseName.ToLower()))
        {
            if (EventSystem.current.currentSelectedGameObject == playerText.gameObject && string.IsNullOrWhiteSpace(playerText.text))
                return;
            _defenseInteractions--;
            string message = playerText.text;
            Debug.Log("Message: " + message);
            llmCharacter.AddPlayerMessage(message);
            playerText.interactable = false;
            await CheckSpecialCharacters(message);
            characterAnimator.HideCurrentCharacter();
            AddToLog(defenseName, message);
            (string nextCharacter, string addInfo) = await _sentenceAnalyzer.Analyze(llmCharacter.chat);
            
            _roundsTimeline.Insert(_round + 1, (nextCharacter, ""));
            EventSystem.current.SetSelectedGameObject(null);
        }
        
        nextButton.interactable = false;
        _ = NextRound();
    }



    private async Task NextRound(bool increment = true)
    {
        if (increment) _round++;
        systemMessages.text = _roundsTimeline[_round].systemMessage;
        
        CheckForJudgeIntervention();

        if (_roundsTimeline[_round].role.ToLower().Contains(defenseName.ToLower()))
        {
            characterAnimator.ShowCharacter(defenseName, ""); // Entra il player

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

            characterAnimator.ShowCharacter(_roundsTimeline[_round].role, "");  // Entra con animazione e poi mostra testo

            string answer = await llmCharacter.ContinueChat(_roundsTimeline[_round].role, SetAIText, AIReplyComplete);
            await CheckSpecialCharacters(answer);
            
            (string nextCharacter, string addInfo) = await _sentenceAnalyzer.Analyze(llmCharacter.chat);
            
            _roundsTimeline.Insert(_round + 1, (nextCharacter, ""));
            
            
            string caseText = GetCaseDescription().GetTotalDescription(false);
            string translatedText = GetTranslatedDescription().GetTotalDescription(false);

            // Se e' l'ultimo round (il giudice emette il verdetto)
            // ROUND FINALE - FASE 1 e FASE 2
            if (_round == _roundsTimeline.Count - 1)
            {
                // --- FASE 1: Sintesi ---
                if (!_finalRoundStepOneDone)
                {
                    _finalRoundSummary = answer.Split(_gameOverCharacter)[0];
                    AddToLog(aiTitle.text, _finalRoundSummary);
                    string finalVerdictPrompt = $"This is the final summary of the case:\n\n{_finalRoundSummary}\n\n" +
                            "Now, based only on this summary, decide whether the defendant is GUILTY or NOT GUILTY. " +
                            "Write only the verdict and end your message with the symbol #VITTORIA or #SCONFITTA.";

                    Debug.Log("[FinalVerdictPrompt]: " + finalVerdictPrompt);

                    llmCharacter.ClearChat(); 
                    llmCharacter.AddSystemMessage(finalVerdictPrompt);
                    _finalRoundStepOneDone = true;

                    await NextRound(false);
                    return;
                }

                // --- FASE 2: Verdetto ---
                string verdict = answer.ToLower();
                string infoRequest = answer.Contains(_gameOverCharacter) ? answer.Split(_gameOverCharacter)[1] : "";

                if (winKeywords.Any(k => verdict.Contains(k)) || infoRequest.Contains("vittoria"))
                {
                    _pendingEndGameMessage = ("HAI VINTO", Color.green);
                }
                else if (loseKeywords.Any(k => verdict.Contains(k)) || infoRequest.Contains("sconfitta"))
                {
                    _pendingEndGameMessage = ("HAI PERSO", Color.red);
                    
                }

                GameSaveSystem.SaveGame("Scene", _round, true, caseText, translatedText);
            }
            else
            {

                GameSaveSystem.SaveGame("Scene", _round, false, caseText, translatedText);
            }


        }

        nextButton.interactable = true;

    }

    private async Task CheckSpecialCharacters(string text)
    {
        
        if (text.Contains(_requestCharacter))
        {
            try
            {
                string infoRequest = text.Split(_requestCharacter)[1].Replace("\n", "");
                if (!string.IsNullOrWhiteSpace(infoRequest))
                {
                    string answer, translatedAnswer;
                    (answer, translatedAnswer) = await _apiManager.RequestAdditionalInfo(_caseDescription.GetTotalDescription(), infoRequest);
                    if (!string.IsNullOrWhiteSpace(answer))
                    {
                        _caseDescription.AddInformation(answer);
                        _translatedDescription.AddInformation(translatedAnswer);
                        
                        llmCharacter.chat[0] = new ChatMessage(){role = "system", content = BuildPrompt()};
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

    void CheckForJudgeIntervention()
    {
        string name = _roundsTimeline[_round].role;
        if(name.ToLower().Contains(attackName.ToLower()))
            if (_attackInteractions <= 0)
                _roundsTimeline[_round] = (judgeName, $"Last message was addressed to {attackName} but they are out of interventions; the Judge must take care of the situation");
            else
                _attackInteractions--;
        else if(name.ToLower().Contains(defenseName.ToLower()))
            if (_defenseInteractions <= 0)
                _roundsTimeline[_round] = (judgeName, $"Last message was addressed to {defenseName} but they are out of interventions; the Judge must take care of the situation");
            else
                _defenseInteractions--;
        
    }

    void AddToLog(string role, string content, string roleColor = "#550505")
    {
        logText.text += $"<b><color={roleColor}>{role}</color></b>: {content}\n\n";
    }
    
    public void SetCurrentRound(int round)
    {
        if (round >= 0 && round < _roundsTimeline.Count)
        {
            _round = round;
            
            systemMessages.text = _roundsTimeline[_round].systemMessage;

        }
       
    }
    
    //TODO create a method to manage the insertion of new turns so that you can easily check if the defense or attack are going to talk and show their corresponding number of remaining interactions
    
    //Property Getter per il retry e per il save
    public CaseDescription GetCaseDescription() => _caseDescription;
    public CaseDescription GetTranslatedDescription() => _translatedDescription;
    public int GetCurrentRound() => _round;

}



public struct CaseDescription
{
    public string title;
    public string playerGoal;
    public string summary;
    public string shortSummary;
    public List<string> evidence;
    public List<string> witnessNames;
    public List<string> witnessDescriptions;
    
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
    

    private string jsonDescription;
    private List<string> additionalInfo;
    private string[] descArray;
    private string[] richArray;
    private int fileID;
    
    
    [JsonConstructor]
    public CaseDescription( string title, string playerGoal, string summary, string shortSummary, List<string> evidence, List<string> witnessNames, List<string> witnessDescriptions, List<string> sectionNames)
    {
        this.title = title;
        this.playerGoal = playerGoal;
        this.summary = summary;
        this.shortSummary = shortSummary;
        this.evidence = evidence.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        this.witnessNames = witnessNames.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        this.witnessDescriptions = witnessDescriptions.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        
        fallbackSectionNames = new List<string>() { "Title", "Player Goal", "Summary", "Short Summary", "Evidence", "Witnesses", "Additional Info" };
        if(sectionNames == null)
            sectionNames = fallbackSectionNames;
        else
            sectionNames.AddRange(fallbackSectionNames.TakeLast(fallbackSectionNames.Count - sectionNames.Count));
    
        this.sectionNames = sectionNames;
        
        descArray = new string[6];
        richArray = new string[6];
        additionalInfo = new List<string>();
        fileID = -1;
        jsonDescription = "";
        UpdateCaseDescription();
    }
    

    public string GetJsonDescription()
    {
        if (string.IsNullOrWhiteSpace(jsonDescription))
            jsonDescription = JsonConvert.SerializeObject(this, Formatting.Indented);
        return jsonDescription;
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
        descArray[5] = $"{GetSectionName(6)}\n" +
                       $"{string.Join("\n", additionalInfo.Select(x => "-" + x))}\n\n";

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
        richArray[5] = $"<b><color=#F64A3E>{GetSectionName(6)}</color></b>\n" +
                       $"{string.Join("\n", additionalInfo.Select(x => "-" + x))}\n\n";
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
