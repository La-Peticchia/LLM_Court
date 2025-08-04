using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LLMUnity;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Serialization;

public class SentenceAnalyzer : MonoBehaviour
{
    public enum Mode
    {
        Analyze,
        Summary,
        FinalVerdict
    }

    private Mode _currentMode = Mode.Analyze;
    
    [SerializeField] LLMCharacter llmCharacter;
    [TextArea(5, 10)] public string analysisPrompt = "You must analyze sentences and extract useful info";
    [TextArea(5, 10)] public string summaryPrompt = "You must summary the whole dialogue from 100 to 150 words";
    [TextArea(5, 10)] public string finalVerdictPrompt = "You are a Judge in a courtroom who is giving the final verdict";
    [SerializeField] string splitCharacters = ">";
    [SerializeField, Range(1, 10)] private int numOfMessages = 5;

    private const string TranslationCharacters = "<trans>";
    private const string TranslationSentence = "Language:\nThe judge speaks italian so your answer must be written in this language";
    
    public void InitializeAnalysis()
    {
        llmCharacter.playerName = "user";
        finalVerdictPrompt = finalVerdictPrompt.Replace(TranslationCharacters, TranslationSentence);
        InitializeChat();
    }
    
    private void InitializeChat()
    {
        llmCharacter.prompt = BuildPrompt();
        llmCharacter.ClearChat();
    }
    
    string BuildPrompt()
    {
        switch (_currentMode)
        {
            case Mode.Analyze:
                return analysisPrompt;
            //return $"{analysisPrompt}\n\n" +
                //       $"Here all the speakers of the courtroom...\n" +
                //       $"Main Speakers:\n-" +
                //       string.Join("\n-", _characters.Take(3).ToArray()) +
                //       $"\nWitness Speakers:\n-" +
                //       string.Join("\n-", _characters.Skip(3).ToArray());
            case Mode.Summary:
                return summaryPrompt;
            case Mode.FinalVerdict:
                return finalVerdictPrompt;
            default:
                return analysisPrompt;
        }
        
    }

    public async Task<string> AnalyzeNextCharacter(List<ChatMessage> chatMessages, string[] characters)
    {
        SwitchMode(Mode.Analyze);
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system").TakeLast(numOfMessages).ToList();
        string lastCharacter = tmpMessages[^1].role;
        characters = characters.Where(x => !x.ToLower().Contains(lastCharacter.ToLower())).ToArray();
        string userPrompt =
            $"Can you extract from the following text:\n{lastCharacter}: {tmpMessages[^1].content}\n\n" +
            $"which one of the following characters:\n-{string.Join("\n-", characters)}\n" +
            $"is {lastCharacter} talking to?\n\n";

        if (tmpMessages.Count > 1)
            userPrompt += $"To better answer my question you can take into account the following dialogue which is prior to the text I specified before:\n{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content}"))}";
                          
        userPrompt += $"You can write the answer in this format: \n< Name of character >";
        
        Debug.Log("Analyze char system prompt:\n" + llmCharacter.prompt);
        
        Debug.Log($"Analyze char user prompt:\n" + userPrompt);
        
        
        string answer = await llmCharacter.Chat(userPrompt);
        Debug.Log("Analyze char answer: " + answer);
        
        var match = Regex.Match(answer,@"<([^>]+)>");
        if (match.Success)
            return match.Groups[1].Value;
        
        return answer;
        
    }

    public async Task<string> AnalyzeInfoNeeded(List<ChatMessage> chatMessages)
    {
        SwitchMode(Mode.Analyze);
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system").TakeLast(numOfMessages).ToList();
        string lastCharacter = tmpMessages[^1].role;

        string userPrompt =
            $"Can you extract from the following text:\n{lastCharacter}: {tmpMessages[^1].content}\n\n" +
            $"what specific piece of factual courtroom-relevant information the {lastCharacter} is requesting?\n\n" +
            $"Follow these steps:\n" +
            $"1 - Determine if the speaker is requesting factual, case-relevant information. If not, return < NULL >\n" +
            $"2 - If yes, extract the information request(s) they are making, ignoring rhetorical or procedural content\n" +
            $"2.5 - If the information request refers to a character (e.g., what someone saw, heard, or did), specify that character’s name in your answer." +
            $"3 - Output only in the format: < answer >\n\n";
        
        if (tmpMessages.Count > 1)
            userPrompt += $"Additional context to consider (prior dialogue):\n{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content}."))}";
                          
        userPrompt += $"Important filtering rules:\n" +
                      $"- Do not extract questions that are rhetorical, procedural, or unrelated to courtroom facts" +
                      $"- Only include requests that could provide specific details or clues to support or refute the case (e.g., times, actions, observations, locations, sounds ...)" +
                      $"- If there’s no factual request, return < NULL >";

        userPrompt += $"Good example\n" +
                      $"- Input:\nProsecutor: \"Signorina Flameheart, ha visto se Alaric Shadowwind tracciava dei simboli sul terreno prima dell’arrivo del drago? Se sì, può descriverli?\"\n" +
                      $"- Output: < A description of any symbols Alaric Shadowwind drew on the ground before the dragon arrived >";
        
        userPrompt += $"Examples of what not to extract:\n" +
                      $"- \"Richiedo il permesso di interrogare la signorina Flameheart.\" → < NULL >\n(this is procedural)" +
                      $"- \"Signor giudice, membri della giuria...\" → < NULL >\n(Opening statement, not an info request)";
        
        Debug.Log($"Analyze info user prompt:\n" + userPrompt);
        
        string answer = await llmCharacter.Chat(userPrompt);
        Debug.Log("Analyze info answer: " + answer);
        
        var match = Regex.Match(answer,@"<([^>]+)>");
        if (match.Success)
            return match.Groups[1].Value;
        
        return answer;
    }

    public async Task<string> AnalyzeGrantInterventions(List<ChatMessage> chatMessages, string[] mainCharacters)
    {
        SwitchMode(Mode.Analyze);
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system").TakeLast(numOfMessages).ToList();
        string lastCharacter = tmpMessages[^1].role;
        string userPrompt =
            $"Can you extract from the following text:\n{lastCharacter}: {tmpMessages[^1].content}\n\n" +
            $"how many interventions {lastCharacter} is granting and who they are giving interventions to?\n\n" +
            $"Follow these steps:\n" +
            $"1 - Determine if the speaker is giving additional interventions. If not, return < NULL >\n" +
            $"2 - If yes, extract the number of granted interventions\n" +
            $"3 - Determine whom character the speaker is talking to; they can be only one of them: {string.Join(" or ", mainCharacters)}" +
            $"4 - Output your answer in the following format: <here you put the number | here you put the character> ";
        
        if (tmpMessages.Count > 1)
            userPrompt += $"Additional context to consider (prior dialogue):\n{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content}."))}";

        Debug.Log("Analyze grant user prompt:\n" + userPrompt);
        
        string answer = await llmCharacter.Chat(userPrompt);
        Debug.Log("Analyze grant answer: " + answer);
        
        var match = Regex.Match(answer,@"<([^>]+)>");
        if (match.Success)
            return match.Groups[1].Value;
        return "NULL";

    }
    
    public async Task<string> Summarize(List<ChatMessage> chatMessages)
    {
        SwitchMode(Mode.Summary);
        
        Debug.Log("Summary prompt:\n" + llmCharacter.prompt);
        
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system").ToList();
        
        string sentence = string.Join("\n",tmpMessages.Select(x => $"{x.role}: {x.content}\n\n"));
        
        Debug.Log("All messages:\n" + sentence);
        string answer = await llmCharacter.Chat(sentence);
        Debug.Log("Summary answer: " + answer);
        llmCharacter.ClearChat();
        
        return answer;
    }

    
    //TODO create the logic in Court.cs to call the FinalVerdict method then debug all the dialogue  
    
    public async Task<string> FinalVerdict(CaseDescription caseDescription, List<ChatMessage> chatMessages, Callback<string> callback = null, EmptyCallback completionCallback = null)
    {
        SwitchMode(Mode.FinalVerdict);

        Debug.Log("Final verdict prompt:\n" + llmCharacter.prompt);
        string dialogueSummary = await Summarize(chatMessages);
        string currentPrompt = $"Case Description:\n{caseDescription.GetTotalDescription(new []{0,2,3,4,5})}\n\nDialogue summary:\n{dialogueSummary}";
        
        Debug.Log("Final verdict user prompt:\n" + currentPrompt);

        string answer = await llmCharacter.Chat(currentPrompt, callback, completionCallback);
        
        Debug.Log("Final verdict answer:\n" + answer);
        
        return answer;
    }
    

    private void SwitchMode(Mode mode)
    {
        if (mode == _currentMode) return;
        
        _currentMode = mode;
        InitializeChat();
    }
    
    
}
