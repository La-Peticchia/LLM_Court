using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Azure;
using Azure.AI.Inference;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class APIInterface : MonoBehaviour
{
    private Uri _endpoint;
    private AzureKeyCredential _credential;
    private string _model = "openai/gpt-4.1";
    private ChatCompletionsClient _client;


    [TextArea(20, 10)] public string prompt;
    
    public static string sectionSplitCharacters = "***";
    public static string subsectionSplitCharacters = "+++";
    static string preferencesReplaceCharacters = "!!!";

    [SerializeField] private bool useDebugPrompt = false;
    
    
    //Debug
    [TextArea(20, 10)] public string dbgCaseDesc;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        _endpoint = new Uri("https://models.github.ai/inference");
        string envPath = Application.dataPath + "/../.env";
        string envVariable = LoadEnv(envPath).Where(x => x.Key == "GITHUB_TOKEN").ToArray()[0].Value;
        _credential = new AzureKeyCredential(envVariable);
        _client = new ChatCompletionsClient(_endpoint, _credential, new AzureAIInferenceClientOptions());

    }



    public async Task<(CaseDescription,CaseDescription)> Request(string preferences = "")
    {

        string currentPrompt = prompt.Replace(preferencesReplaceCharacters,
            preferences != "" ? "I want you to take into account these preferences/topics: " + preferences : ""); 

        var requestOptions = new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatRequestSystemMessage("Just answer the user requests, there's no need to add anything else"),
                new ChatRequestUserMessage(currentPrompt),
            },
            Temperature = 1f,
            NucleusSamplingFactor = 1f,
            Model = _model
        };
        
        string[] descriptions;
        if (useDebugPrompt)
        {
            descriptions = dbgCaseDesc.Split("^^^", StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            try
            {
                Response<ChatCompletions> response = await _client.CompleteAsync(requestOptions);
                Debug.Log(response.Value.Content); 
                descriptions = response.Value.Content.Split("^^^", StringSplitOptions.RemoveEmptyEntries);
            }
            catch 
            {
                Debug.LogWarning("Invalid format generated on API call: \n");
                return (new CaseDescription(), new CaseDescription());
            }
        }
        
        return (new CaseDescription(descriptions[0], sectionSplitCharacters, subsectionSplitCharacters), new CaseDescription(descriptions[1], sectionSplitCharacters, subsectionSplitCharacters));
        
    }

    public async Task<(string,string)> Request(string totalCaseDescription, string addRequest)
    {
        var addRequestOptions = new ChatCompletionsOptions()
        {
            Messages = 
            {
                //new ChatRequestSystemMessage("The user will give you a court case description, you must satisfy the user's needs with a short answer (less than 50 words) and without adding anything else. You must answer both in english and italian and split those answers with these characters: ^^^"),
                new ChatRequestSystemMessage("I will give you a court case description, the user will ask something not present in the description and you must make things up to satisfy their request. " +
                                             "Your answer must include ONLY THE INFORMATION REQUIRED BY THE USER, DO NOT INTERACT WITH THEM IN ANY OTHER WAY. " +
                                             "The answer must be less than 50 words. " +
                                             "If the user does not require information just answer back with this word: NULL\n" +
                                             "Finally you must answer both in english and italian and split those answers with these characters: ^^^\nCase Description\n" + totalCaseDescription),
                new ChatRequestUserMessage(addRequest),
            },
            Temperature = 1f,
            NucleusSamplingFactor = 1f,
            Model = _model
        };
        
        Response<ChatCompletions> response = await _client.CompleteAsync(addRequestOptions);
        Debug.Log("Answer:\n" + response.Value.Content);
        if (response.Value.Content == "NULL")
            return ("", "");
        
        string[] split = response.Value.Content.Split("^^^", StringSplitOptions.RemoveEmptyEntries);
        return (split[0],split[1]);
    }

    public static string RemoveSplitters(string inDescription)
    {
        return inDescription.Replace(sectionSplitCharacters, "").Replace(subsectionSplitCharacters, "");
    }
    
    
    public static Dictionary<string, string> LoadEnv(string path)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
                result[parts[0]] = parts[1];
        }
        return result;
    }
    
}
