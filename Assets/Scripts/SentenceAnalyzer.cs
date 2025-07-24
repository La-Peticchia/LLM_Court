using System;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;

public class SentenceAnalyzer : MonoBehaviour
{
    
    [SerializeField] LLMCharacter llmCharacter;
    [TextArea(5, 10)] public string mainPrompt = "You must analyze sentences and extract useful info";
    [SerializeField] string splitCharacters = ">";
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
        Debug.Log(llmCharacter.gameObject.name + llmCharacter.prompt);
        llmCharacter.ClearChat();
    }
    
    string BuildPrompt()
    {
        return $"{mainPrompt}\n\n" +
               $"Here all the characters of the courtroom:\n-" +
               string.Join("\n-", _characters);
    }

    public async Task<(string chosenCharacter, string addInfoRequest)> Analyze(string sentence)
    {

        try
        {
            string[] answer = (await llmCharacter.Chat(sentence)).Split(splitCharacters);
            llmCharacter.ClearChat();
            return (answer[1], answer[2]);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e.Message);
            return ("", "");
        }
        
        
    }
    
}
