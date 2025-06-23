using System;
using UnityEngine;
using Azure;
using Azure.AI.Inference;
public class APIInterface : MonoBehaviour
{
    private Uri _endpoint;
    private AzureKeyCredential _credential;
    private string _model = "openai/gpt-4.1";
    private ChatCompletionsClient _client;
    
    //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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _endpoint = new Uri("https://models.github.ai/inference");
        _credential = new AzureKeyCredential("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");
        _client = new ChatCompletionsClient(_endpoint, _credential, new AzureAIInferenceClientOptions());
        
        var requestOptions = new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatRequestSystemMessage(""),
                new ChatRequestUserMessage("What is the capital of France?"),
            },
            Temperature = 1f,
            NucleusSamplingFactor = 1f,
            Model = _model
        };
        
        Response<ChatCompletions> response = _client.Complete(requestOptions);
        Debug.Log(response.Value.Content);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
