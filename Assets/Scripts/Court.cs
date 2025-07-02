using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLMUnity;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
public class Court : MonoBehaviour
{
    //References
    [SerializeField] private LLMCharacter llmCharacter;
    [SerializeField] private InputField playerText;
    [SerializeField] private Text aiText;
    [SerializeField] private Text aiTitle;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI systemMessages;
    [SerializeField] private TextMeshProUGUI caseDescriptionText;
    [SerializeField] private Button nextButton;
    private APIInterface apiManager;
    [SerializeField] RunJets runJets;
    [SerializeField] public Button micButton;
    public MicrophoneInput micInput;
    [SerializeField] private CharacterAnimator characterAnimator;

    //Names
    [SerializeField] private string defenseName = "Defense";
    [SerializeField] private string attackName = "Attack";
    [SerializeField] private string judgeName = "Judge";

    //Prompts
    [TextArea(5, 10)] public string mainPrompt = "A court case where the AI takes control of several characters listed below";
    [TextArea(5, 10)] public string judgePrompt = "The goal of Judge is to give the defendant's final sentence by listening to the dialogue";
    [TextArea(5, 10)] public string attackPrompt = "The goal of Attack is proving to the Judge that the defendant is guilty";

    private List<(string role, string systemMessage)> _roundsTimeline;
    private CaseDescription _caseDescription, _translatedDescription;
    private int _round;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        playerText.interactable = false;
        playerText.gameObject.SetActive(false);
        micButton.interactable = false;
        nextButton.interactable = false;

        apiManager = FindFirstObjectByType<APIInterface>();
        (_caseDescription, _translatedDescription) = await apiManager.Request();

        characterAnimator.AssignDynamicPrefabs(
        _caseDescription.witnesses.Keys.ToList(),
        attackName);

        InitializePrompt();
        InitializeRounds();
        caseDescriptionText.text = $"<b><color=#F64A3E>{_translatedDescription.sectionTitles[0]}</color></b>\n" +
                                   $"{_translatedDescription.title}\n\n" +
                                   $"<b><color=#F64A3E>{_translatedDescription.sectionTitles[1]}</color></b>\n" +
                                   $"{_translatedDescription.summary}\n\n" +
                                   $"<b><color=#F64A3E>{_translatedDescription.sectionTitles[2]}</color></b>\n" +
                                   $"{string.Join("\n",_translatedDescription.clues.Select(x => "-" + x))}\n\n" +
                                   $"<b><color=#F64A3E>{_translatedDescription.sectionTitles[3]}</color></b>\n" +
                                   $"{string.Join("\n",_translatedDescription.witnesses.Select(x =>  $"-<i><color=#550505>{x.Key}</color></i>: {x.Value}").ToArray())}\n\n";
            
        
        playerText.onSubmit.AddListener(OnInputFieldSubmit);
        nextButton.onClick.AddListener(OnNextButtonClick);
        llmCharacter.playerName = defenseName;
        
        await llmCharacter.llm.WaitUntilReady();
        nextButton.interactable = true;
        
        
        
        //NextRound(false);
        
    }

    private void InitializePrompt()
    {
        llmCharacter.prompt = $"{mainPrompt}\n{judgeName} - {judgePrompt}\n{attackName} - {attackPrompt}\n\nWitnesses:";

        foreach (var item in _caseDescription.witnesses)
            llmCharacter.prompt += $"{item.Key} - {item.Value}\n";

        llmCharacter.prompt += $"\n {APIInterface.RemoveSplitters(string.Join("",_caseDescription.totalDescription.Split(APIInterface.sectionSplitCharacters).Take(3).ToArray()))}";
        
        Debug.Log(llmCharacter.prompt);
        
        
        llmCharacter.ClearChat();
    }

    private void InitializeRounds()
    {
        _roundsTimeline = new List<(string role, string systemMessage)>
        {
            (" "," "),
            (judgeName, $"Now the {judgeName} introduces the court case then passes the word to {attackName}"),
            (attackName, $"Now the {attackName} exposes the clues trying to convince the judge"),
            (defenseName, $"Now the {defenseName} dismantle the evidence trying to convince the judge")
        };

        foreach (var item in _caseDescription.witnesses)
        {
            _roundsTimeline.Add((attackName, $"Now the {attackName} questions {item.Key} about the case"));
            _roundsTimeline.Add((item.Key, ""));
        }
        
        foreach (var item in _caseDescription.witnesses)
        {
            _roundsTimeline.Add((defenseName, $"Now the {defenseName} questions {item.Key} about the case"));
            _roundsTimeline.Add((item.Key, ""));
        }
        
        _roundsTimeline.Add((attackName, $"Now it's the {attackName} last intervention"));
        _roundsTimeline.Add((defenseName, $"Now it's the {defenseName} last intervention"));
        _roundsTimeline.Add((judgeName, $"Now the {judgeName} give their final sentence"));
    }

    private void OnInputFieldSubmit(string message)
    {
        playerText.interactable = false;
        aiText.text = "...";
        llmCharacter.AddPlayerMessage(message);
        logText.text += $"<b><color=#550505>{_roundsTimeline[_round].role}</color></b>: {message}\n\n";
        characterAnimator.HideCurrentCharacter();
        _ = NextRound();
    }
    
    public void SetAIText(string text)
    {
        aiText.text = text;
    }
    
    public void AIReplyComplete()
    {
        nextButton.interactable = true;
        //runJets.TextToSpeech(aiText.text);
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
        }
        else
        {

            playerText.interactable = false;
            playerText.gameObject.SetActive(false);

            micInput.EnableMicInput(false);

            aiTitle.text = _roundsTimeline[_round].role;
            string systemMessage = _roundsTimeline[_round].systemMessage;
            if(systemMessage != "")
                llmCharacter.AddSystemMessage(systemMessage);

            
            characterAnimator.ShowCharacter(_roundsTimeline[_round].role, "");  // Entra con animazione e poi mostra testo

            string answer = await llmCharacter.ContinueChat(_roundsTimeline[_round].role ,SetAIText, AIReplyComplete);


            logText.text += $"<b><color=#550505>{_roundsTimeline[_round].role}</color></b>: {answer}\n\n";
        }

    }
    
    private async void OnNextButtonClick()
    {
        nextButton.interactable = false;
        await NextRound();
    }

}

public struct CaseDescription
{
    public string title;
    public string summary;
    public string[] clues;
    public Dictionary<string, string> witnesses;
    public string totalDescription;
    public string[] sectionTitles;

    public CaseDescription(string title, string summary, string[] clues, Dictionary<string, string> witnesses, string totalDescription, string[] sectionTitles)
    {
        this.title = title;
        this.summary = summary;
        this.clues = clues;
        this.witnesses = witnesses;
        this.totalDescription = totalDescription;
        this.sectionTitles = sectionTitles;
    }
}
