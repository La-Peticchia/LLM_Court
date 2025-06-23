using System.Collections.Generic;
using System.Threading.Tasks;
using LLMUnity;
using TMPro;
using UnityEngine;
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
    [SerializeField] RunJets runJets;
    [SerializeField] public Button micButton;          
    [SerializeField] public MicrophoneInput micInput;  

    //Names
    [SerializeField] private string defenseName = "Defense";
    [SerializeField] private string attackName = "Attack";
    [SerializeField] private string judgeName = "Judge";
    [SerializeField] private string[] witnessNames = new string[] { "Witness1" };

    //Prompts
    [TextArea(5, 10)] public string mainPrompt = "A court case where the AI takes control of several characters listed below";
    [TextArea(5, 10)] public string judgePrompt = "The goal of Judge is to give the defendant's final sentence by listening to the dialogue";
    [TextArea(5, 10)] public string attackPrompt = "The goal of Attack is proving to the Judge that the defendant is guilty";
    [TextArea(5, 10)] public string caseDescription = "Mr. Joe killed Mrs. Mama yesterday";

    [SerializeField]
    private string[] witnessPrompts;

    private List<(string role, string systemMessage)> _roundsTimeline;
    public bool PlayerCanAct => _roundsTimeline[_round].role == defenseName;

    private int _round;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        InitializePrompt();
        InitializeRounds();
        caseDescriptionText.text = caseDescription;
        playerText.onSubmit.AddListener(OnInputFieldSubmit);
        nextButton.onClick.AddListener(OnNextButtonClick);
        llmCharacter.playerName = defenseName;
        
        playerText.interactable = false;
        micButton.interactable = false;
        nextButton.interactable = false;
        await llmCharacter.llm.WaitUntilReady();
        nextButton.interactable = true;
        
        
        
        //NextRound(false);
        
    }

    private void InitializePrompt()
    {
        llmCharacter.prompt = $"{mainPrompt}\n{judgeName} - {judgePrompt}\n{attackName} - {attackPrompt}\n";

        for (int i = 0; i < witnessNames.Length; i++)
        {
            llmCharacter.prompt += $"{witnessNames[i]} - {witnessPrompts[i]}\n";
        }

        llmCharacter.prompt += $"\nCase Summary - {caseDescription}";
        
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

        foreach (var item in witnessNames)
        {
            _roundsTimeline.Add((attackName, $"Now the {attackName} questions {item} about the case"));
            _roundsTimeline.Add((item, ""));
        }
        
        foreach (var item in witnessNames)
        {
            _roundsTimeline.Add((defenseName, $"Now the {defenseName} questions {item} about the case"));
            _roundsTimeline.Add((item, ""));
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
        _ = NextRound();
    }
    
    public void SetAIText(string text)
    {
        aiText.text = text;
    }
    
    public void AIReplyComplete()
    {
        nextButton.interactable = true;
        runJets.TextToSpeech(aiText.text);
    }

    private async Task NextRound(bool increment = true)
    {
        if (increment) _round++;
        systemMessages.text = _roundsTimeline[_round].systemMessage;

        if (_roundsTimeline[_round].role == defenseName)
        {
            playerText.interactable = true;
            playerText.text = "";
            playerText.Select();
        }
        else
        {
            aiTitle.text = _roundsTimeline[_round].role;
            string systemMessage = _roundsTimeline[_round].systemMessage;
            if(systemMessage != "")
                llmCharacter.AddSystemMessage(systemMessage);
            
            string answer = await llmCharacter.ContinueChat(_roundsTimeline[_round].role ,SetAIText, AIReplyComplete);
            logText.text += $"<b><color=#550505>{_roundsTimeline[_round].role}</color></b>: {answer}\n\n";
        }

        if (PlayerCanAct)
        {
            playerText.interactable = true;
            micButton.interactable = true;
        }
        else
        {
            playerText.interactable = false;
            micButton.interactable = false;
        }

    }
    
    private async void OnNextButtonClick()
    {
        nextButton.interactable = false;
        await NextRound();
    }

}
