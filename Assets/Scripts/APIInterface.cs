using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Azure;
using Azure.AI.Inference;
using Newtonsoft.Json;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class APIInterface : MonoBehaviour
{
    private enum ModelType
    {
        Gpt,
        Llama,
        DeepSeek,
        Grok
    }
    
    private Uri _endpoint;
    private AzureKeyCredential _credential;
    
    [SerializeField]private ModelType caseGenerationModel = ModelType.Gpt;
    [SerializeField]private ModelType additionalInfoModel = ModelType.Llama;
    
    private ChatCompletionsClient _client;


    [TextArea(20, 10)] public string prompt;
    //[TextArea(10, 10)] public string witnessPrompt;
    [TextArea(5, 10)] public string formatToRepeat;
    [TextArea(10, 10)] public string AdditionalInfoPrompt;
    //Special Characters
    private const string SectionSplitCharacters = "***";
    private const string SubsectionSplitCharacters = "+++";
    private const string ReplaceCharacters = "!!!";
    private const string FormatRepeatCharacters = "|||";
    private const string TranslationCharacters = "<trans>";
    private const string NumReplaceCharacters = "<num>";
    
    
    [SerializeField]
    private string[] specialCharactersToRemove;

    
    //Task cancellation timeouts
    [SerializeField]
    private int mainRequestTimeout = 20000;
    [SerializeField]
    private int addRequestTimeout = 5000;
    
    public bool useDebugPrompt = false;
    
    //Debug
    [TextArea(20, 10)] public string dbgCaseDesc;

    ChatCompletionsOptions _chatOptions;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        _endpoint = new Uri("https://models.github.ai/inference");
        string envPath = Application.dataPath + "/../.env";
        string envVariable = LoadEnv(envPath).Where(x => x.Key == "GITHUB_TOKEN").ToArray()[0].Value;
        _credential = new AzureKeyCredential(envVariable);
        _client = new ChatCompletionsClient(_endpoint, _credential, new AzureAIInferenceClientOptions());




        _chatOptions = new ChatCompletionsOptions()
        {
            ResponseFormat = ChatCompletionsResponseFormat.CreateJsonFormat(),
            Model = GetModel(caseGenerationModel),
            Temperature = 1f,
            NucleusSamplingFactor = 1f,
            FrequencyPenalty = 2,
            PresencePenalty = 1,
            MaxTokens = 2048
        };
    }

    private string GetModel(ModelType type)
    {
        return type switch
        {
            ModelType.Gpt => "openai/gpt-4.1",
            //ModelType.Gpt => "openai/gpt-5",
            ModelType.Llama => "meta/Llama-4-Scout-17B-16E-Instruct",
            //ModelType.Llama => "meta/Meta-Llama-3.1-405B-Instruct";
            ModelType.DeepSeek => "deepseek/DeepSeek-V3-0324",
            ModelType.Grok => "xai/grok-3",
            _ => "openai/gpt-4.1"
        };
    }
    
    //TODO rivedere il prompt principale per poter generare dei casi un po' meno misteriosi
    
    public async Task<CaseDescription> RequestCaseDescription(string preferences = "", bool translation = false, int seed = 0)
    {
        
        CaseDescription tmpDescription;
        
        if (useDebugPrompt)
        {
            string cleanResponse = RemoveSpecialCharacters(dbgCaseDesc);

            CaseDescription[] dbgDescriptions = JsonConvert.DeserializeObject<CaseDescription[]>(cleanResponse);
            tmpDescription = dbgDescriptions[^1];
        }
        else
        {
            try
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(mainRequestTimeout);

                #region CallForDescription
                int rand = Random.Range(2,5);
                string currentPrompt = 
                    prompt.Replace(ReplaceCharacters, preferences != "" ? "I want you to take into account these preferences/topics: " + preferences : "")
                    .Replace(TranslationCharacters, (translation) ? "Write the value of each JSON Key in italian; the language should not interfere with the case generation, so proper names are not affected by the language" : "")
                    .Replace(NumReplaceCharacters, rand.ToString())
                    .Replace(FormatRepeatCharacters, GetWitnessesFormat(rand));

                Debug.Log(currentPrompt);
                
                Debug.Log("Seed: " + seed);
                var requestOptions = _chatOptions;
                requestOptions.Messages = new List<ChatRequestMessage>()
                {
                        new ChatRequestSystemMessage("You are a reliable generator of fictional courtroom case data. Always respond with valid JSON following strict formatting rules. Never write any explanation, commentary, or prose outside of JSON. All keys must match exactly. Maintain narrative quality within a rigid structure. You must keep the same JSON structure specified by the user"),
                        new ChatRequestUserMessage(currentPrompt)
                };
                requestOptions.Seed = seed;
                

                
                Response<ChatCompletions> response = await _client.CompleteAsync(requestOptions, cts.Token);
                Debug.Log(response.Value.Content); 
                
                #endregion
                
                string cleanResponse = RemoveSpecialCharacters(response.Value.Content);
                tmpDescription = JsonConvert.DeserializeObject<CaseDescription>(cleanResponse);
                
                //TODO
                //- organize the JSON object creation in 2 phases: the first one you create the case description, the second one you create the witnesses
                //- Add 2 more names (witnesses + additional information) to the "sectionNames" JSON Key in prompt
                
            }
            catch(Exception e) 
            {
                
                Debug.LogWarning("Error on API call: " + e.Message);
                return null;
            }
        }
        
        return tmpDescription;
        
    }

    public async Task<string> RequestTranslation(string jsonDescription, string language = "english", int seed = 0)
    {
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(mainRequestTimeout);

        string currentPrompt = "I want you to translate this JSON object in " + language + ":\n\n" + jsonDescription;
        Debug.Log("Seed: " + seed);
        
        Debug.Log("Translation request: \n" + currentPrompt);
                
        var requestOptions = _chatOptions;
        requestOptions.Messages = new List<ChatRequestMessage>()
        {
            new ChatRequestSystemMessage("You are a reliable language translator and your duty is to translate the JSON Keys values in the language specified by the user. Always respond with valid JSON following strict formatting rules. DO NOT INCLUDE any root key like \"translation\". The output must be a single, anonymous JSON object."),
            new ChatRequestUserMessage(currentPrompt)
        };
        requestOptions.Seed = seed;
        
        try
        {
            Response<ChatCompletions> response = await _client.CompleteAsync(requestOptions, cts.Token);
            Debug.Log("Translation result: \n" + response.Value.Content);
            return RemoveSpecialCharacters(response.Value.Content);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Error on API call: " + e.Message);
            return "";
        }
                
    }
    
//    public async Task<Witness[]> RequestWitnesses(string caseDescription, int seed = 0)
//    {
//        var cts = new CancellationTokenSource();
//        cts.CancelAfter(mainRequestTimeout);
//
//        int rand = Random.Range(2,5);
//        string currentPrompt = witnessPrompt.Replace(ReplaceCharacters, caseDescription).Replace(FormatRepeatCharacters, GetWitnessesFormat(rand)).Replace(NumReplaceCharacters, rand.ToString());
//        Debug.Log("Seed: " + seed);
//        Debug.Log("Witness request:\n" +currentPrompt); 
//        
//        var requestOptions = _chatOptions;
//        requestOptions.Messages = new List<ChatRequestMessage>()
//        {
//            new ChatRequestSystemMessage("You are a reliable generator of fictional courtroom case witnesses data. Always respond with valid JSON following strict formatting rules. Never write any explanation, commentary, or prose outside of JSON. ADD the root key \"witnesses\" to introduce the witnesses array All keys must match exactly. Maintain narrative quality within a rigid structure."),
//            new ChatRequestUserMessage(currentPrompt)
//        };
//        requestOptions.Seed = seed;
//        
//        try
//        {
//            Response<ChatCompletions> response = await _client.CompleteAsync(requestOptions, cts.Token);
//            string cleanResponse = RemoveSpecialCharacters(response.Value.Content);
//            Debug.Log("Witness response:\n" + response.Value.Content); 
//            
//            var root = JObject.Parse(cleanResponse);
//            return root.ContainsKey("witnesses") ? root["witnesses"]?.ToObject<Witness[]>() : JsonConvert.DeserializeObject<Witness[]>(cleanResponse);
//        }
//        catch (Exception e)
//        {
//            Debug.LogWarning("Error on API call: " + e.Message);
//            return null;
//        }
//    }

    public async Task<(string,string)> RequestAdditionalInfo(string totalCaseDescription, string addRequest)
    {
        if(useDebugPrompt) return ("","");
        
        
        var requestOptions = _chatOptions;
        requestOptions.Messages = new List<ChatRequestMessage>()
        {
            new ChatRequestSystemMessage(AdditionalInfoPrompt + "\n\nCase Description:\n" + totalCaseDescription),
            new ChatRequestUserMessage(addRequest)
        };
        requestOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateTextFormat();
        requestOptions.Model = GetModel(additionalInfoModel);
        
        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(addRequestTimeout);
            
            Response<ChatCompletions> response = await _client.CompleteAsync(requestOptions, cts.Token);
            
            Debug.Log("Answer:\n" + response.Value.Content);
            if (response.Value.Content.Contains("NULL"))
                return ("", "");
            
            string[] split = response.Value.Content.Split("^^^", StringSplitOptions.RemoveEmptyEntries);
            return (split[0],split[1]);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Error on API call: " + e.Message);
            return ("", "");
        }
        
        
    }

    private string GetWitnessesFormat(int repeatNum)
    {
        string format = "";
        for (int i = 0; i < repeatNum; i++)
        {
            format += formatToRepeat + "\n";
        }
        
        return format;
    }

    private string RemoveSpecialCharacters(string s)
    {
        return specialCharactersToRemove.Aggregate(s, (current, item) => current.Replace(item, ""));
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
