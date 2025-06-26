using System;
using System.Collections.Generic;
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
    private ChatCompletionsOptions _requestOptions;

    [TextArea(20, 10)] public string prompt;

    public static string sectionSplitCharacters = "***";
    public static string subsectionSplitCharacters = "+++";
    
    
    //Debug
    [TextArea(20, 10)] public string dbgCaseDesc;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        _endpoint = new Uri("https://models.github.ai/inference");
        _credential = new AzureKeyCredential(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
        _client = new ChatCompletionsClient(_endpoint, _credential, new AzureAIInferenceClientOptions());
        _requestOptions = new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatRequestSystemMessage("Just answer the user requests, there's no need to add anything else"),
                new ChatRequestUserMessage(prompt),
            },
            Temperature = 1f,
            NucleusSamplingFactor = 1f,
            Model = _model
        };

        //OnGenButtonClick();
        
    }



    public async Task<(CaseDescription,CaseDescription)> Request()
    {
        
        Response<ChatCompletions> response = await _client.CompleteAsync(_requestOptions);
        Debug.Log(response.Value.Content);
        string[] descriptions = response.Value.Content.Split("^^^", StringSplitOptions.RemoveEmptyEntries);
        //string[] descriptions = dbgCaseDesc.Split("^^^", StringSplitOptions.RemoveEmptyEntries);
        
        return (BuildCaseDescription(descriptions[0]), BuildCaseDescription(descriptions[1]));
        
    }


    CaseDescription BuildCaseDescription(string description)
    {
        
        string[] answer = description.Split(sectionSplitCharacters, StringSplitOptions.RemoveEmptyEntries);
        Dictionary<string, string> witnessDictionary = new Dictionary<string, string>();
        string[] witnesses = answer[3].Split(">")[1].Split(subsectionSplitCharacters, StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in witnesses)
        {
            if(string.IsNullOrWhiteSpace(item)) continue;
            string[] itemSplit = item.Split(":");
            witnessDictionary.Add(itemSplit[0].Replace("-","").Replace("\n",""), itemSplit[1].Replace("\n",""));
        }

        return new CaseDescription(answer[0].Split(">")[1].Replace("\n",""), 
            answer[1].Split(">")[1].Replace("\n",""),
            answer[2].Split(">")[1].Split(subsectionSplitCharacters, StringSplitOptions.RemoveEmptyEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Replace("-","").Replace("\n","")).ToArray(), 
            witnessDictionary, 
            description,
            answer.Select(x => x.Split(">")[0].Replace("\n", "")).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray());
        
    }

    public static string RemoveSplitters(string inDescription)
    {
        return inDescription.Replace(sectionSplitCharacters, "").Replace(subsectionSplitCharacters, "");
    }
    
    
}
