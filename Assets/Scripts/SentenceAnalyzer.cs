using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLMUnity;
using UnityEditor.Rendering;
using UnityEngine;

public class SentenceAnalyzer : MonoBehaviour
{
    
    [SerializeField] LLMCharacter llmCharacter;
    [TextArea(5, 10)] public string mainPrompt = "You must analyze sentences and extract useful info";
    [SerializeField] string splitCharacters = ">";
    [Range(1, 10)] private const int NumOfMessages = 5;

    private string[] _characters;
    
    
    
    public void InitializeAnalysis(string[] characters)
    {
        _characters = characters;
        llmCharacter.playerName = "user";
        InitializeChat();
    }
    
    private void InitializeChat()
    {
        llmCharacter.prompt = BuildPrompt();
        llmCharacter.ClearChat();
    }
    
    string BuildPrompt()
    {
        return $"{mainPrompt}\n\n" +
               $"Here all the characters of the courtroom:\n-" +
               string.Join("\n-", _characters);
    }

    public async Task<(string chosenCharacter, string addInfoRequest)> Analyze(List<ChatMessage> chatMessages)
    {

        Debug.Log("Analyze prompt:\n" + llmCharacter.prompt);
        
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system").TakeLast(NumOfMessages).ToList();

        string sentence = string.Join("\n",tmpMessages.Select(x => $"{x.role}: {x.content}\n\n"));
        
        Debug.Log($"Last {NumOfMessages} messages:\n" + sentence);
        
        
            string answer = await llmCharacter.Chat(sentence);
            string[] split = answer.Split(splitCharacters);
            Debug.Log("Analyze answer: " + answer);
            llmCharacter.ClearChat();
        
        if(split.Length == 3)
            return (split[1], split[2]);
        if (split.Length == 2)
            return (split[1], "");
        
        Debug.LogWarning("Split failed");
        return ("", "");
        
    }
    
}
