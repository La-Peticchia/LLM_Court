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

    public void InitializeAnalysis()
    {
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
        switch (_currentMode)
        {
            case Mode.Analyze:
                return analysisPrompt;
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

//    public async Task<string> AnalyzeInfoNeeded(List<ChatMessage> chatMessages, CaseDescription caseDescription)
//    {
//        SwitchMode(Mode.Analyze);
//        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system" && x.role != "Case Description").TakeLast(numOfMessages).ToList();
//        string lastCharacter = tmpMessages[^1].role;
//
//        string userPrompt =
//            $"Can you extract from the following text:\n{lastCharacter}: {tmpMessages[^1].content.Split("*")[0].Split("<")[0]}\n\n" +
//            $"what specific piece information related to the case description and witnesses specified below the {lastCharacter} is requesting?\n\n" +
//            $"Follow these steps:\n" +
//            $"1 - Determine if the speaker is requesting factual, case-relevant information. If not, return < NULL >\n" +
//            $"2 - If yes, extract the information request(s) they are making, ignoring rhetorical or procedural content\n" +
//            $"2.5 - If the information request refers to a character (e.g., what someone saw, heard, or did), specify that character’s name in your answer; you MUST NOT use pronouns.\n" +
//            $"3 - Output only in the format: < answer >\n\n";
//        
//        userPrompt += $"Additional context to consider (case description):\n{caseDescription.GetTotalDescription(new []{0,2,3,4})}";
//        
//        if (tmpMessages.Count > 1)
//            userPrompt += $"\n\nAdditional context to consider (prior dialogue):\n{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content.Split("*")[0].Split("<")[0]}."))}";
//                          
//        userPrompt += $"\n\nImportant filtering rules:\n" +
//                      $"- Do not extract requests that are too generic (e.g. \"More information\")" +
//                      $"- Do not extract request(s) of additional interventions or questions " +
//                      $"- Do not extract questions that are rhetorical, procedural, or unrelated to courtroom facts" +
//                      $"- Only include requests that could provide specific details or clues to support or refute the case (e.g., times, actions, observations, locations, sounds ...)" +
//                      $"- If there’s no factual request, return < NULL >";
//
//        userPrompt += $"\n\nGood example\n" +
//                      $"- Input:\nProsecutor: \"Signorina Flameheart, ha visto se Alaric Shadowwind tracciava dei simboli sul terreno prima dell’arrivo del drago? Se sì, può descriverli?\"\n" +
//                      $"- Output: < A description of any symbols Alaric Shadowwind drew on the ground before the dragon arrived >";
//        
//        userPrompt += $"\n\nExamples of what not to extract:\n" +
//                      $"- \"Richiedo il permesso di interrogare la signorina Flameheart.\" → < NULL >\n(this is procedural)" +
//                      $"- \"Signor giudice, membri della giuria...\" → < NULL >\n(Opening statement, not an info request)";
//        
//        Debug.Log($"Analyze info user prompt:\n" + userPrompt);
//        
//        string answer = await llmCharacter.Chat(userPrompt);
//        Debug.Log("Analyze info answer: " + answer);
//        
//        var match = Regex.Match(answer,@"<([^>]+)>");
//        if (match.Success)
//            return match.Groups[1].Value;
//        
//        return answer;
//    }

    public async Task<string> AnalyzeInfoNeeded(List<ChatMessage> chatMessages, CaseDescription caseDescription)
    {
        SwitchMode(Mode.Analyze);
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system" && x.role != "Case Description").TakeLast(numOfMessages).ToList();
        string lastCharacter = tmpMessages[^1].role;

        string userPrompt = "Analyze the user prompt for case-related factual information requests.\n";

        userPrompt +=
            $"\n# Instructions\nGiven the following input:\n\"{lastCharacter}: {tmpMessages[^1].content.Split("*")[0].Split("<")[0]}\"\n\nIdentify any specific factual information the {lastCharacter} is seeking about the case description or witnesses.\n\nContext:\n- Case description: \"{caseDescription.GetTotalDescription(new []{0,2,3,4})}\"\n- Prior dialogue: \"{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content.Split("*")[0].Split("<")[0]}."))}\"\n";

        userPrompt += "\nFollow these steps:\n1. Determine whether the speaker is making a factual, case-related information request. If not, respond as per Output Format guidelines.\n2. If so, extract only the core factual information requests and ignore rhetorical or procedural content.\n3. If the request mentions a character (e.g., what someone saw, heard, or did), specify that character's full name in your output; avoid pronouns.\n4. Disregard requests that are generic (e.g., \"More information\"), procedural (e.g., requesting permission), or irrelevant to the facts of the case.\n5. Only include requests that seek specific details or clues important for confirming or disproving any aspect of the case (e.g., actions, times, locations, observations, sounds).\n6. If the input given before, is empty, malformed, or contains no factual information request, respond per Output Format.\n\nAfter extraction, validate that the output correctly reflects whether a factual request was found and that it is concise and specific. If validation fails, correct the output before returning.\n" +
                      "\n# Good Extraction Example\nInput:\nProsecutor: \"Signorina Flameheart, ha visto se Alaric Shadowwind tracciava dei simboli sul terreno prima dell’arrivo del drago? Se sì, può descriverli?\"\nOutput:\nA description of any symbols Alaric Shadowwind drew on the ground before the dragon arrived.\n" +
                      "\n# Do Not Extract These (Examples)\nInput: \"Richiedo il permesso di interrogare la signorina Flameheart.\"\nOutput:\nNULL\n" +
                      "\nInput: \"Signor giudice, membri della giuria...\"\nOutput:\nNULL\n" +
                      "\n# Output Format\n- If a valid, case-relevant request is present, return a concise description of what is being asked for, in plain text.\n- If not, or if input is empty/unrelated/malformed, state \"NULL\"\n- Return only this textual result, without JSON formatting or extra explanation.\n";
        
        
        Debug.Log($"Analyze info user prompt:\n" + userPrompt);
        
        string answer = await llmCharacter.Chat(userPrompt);
        Debug.Log("Analyze info answer: " + answer);
        
        var match = Regex.Match(answer,@"<([^>]+)>");
        if (match.Success)
            return match.Groups[1].Value;
        
        return answer;
    }

    
    //public async Task<(int, string)> AnalyzeGrantInterventions(List<ChatMessage> chatMessages, string[] mainCharacters)
    //{
    //    SwitchMode(Mode.Analyze);
    //    List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system" && x.role != "Case Description").TakeLast(numOfMessages).ToList();
    //    string lastCharacter = tmpMessages[^1].role;
    //    string userPrompt =
    //        $"Can you extract from the following text:\n{lastCharacter}: {tmpMessages[^1].content.Split("*")[0].Split("<")[0]}\n\n" +
    //        $"how many interventions {lastCharacter} is granting and who they are giving interventions to?\n\n" +
    //        $"Follow these steps:\n" +
    //        $"1 - Determine if the speaker is giving additional interventions. \n" +
    //        $"2 - If yes, extract the number of granted interventions\n" +
    //        $"3 - Determine whom character the speaker is talking to; they can be only one of them: {string.Join(" or ", mainCharacters)} or NULL" +
    //        $"4 - Output your answer in the following format: [here you put the number (put 0 if no interventions are granted) | here you put the character name (put NULL if no interventions are granted)] ";
    //
    //    
    //    if (tmpMessages.Count > 1)
    //        userPrompt += $"\n\nAdditional context to consider (prior dialogue):\n{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content.Split("*")[0].Split("<")[0]}."))}";
    //
    //    userPrompt += $"\n\nImportant notes:\n" +
    //                  $"- Remember that {lastCharacter} must explicitly grant a specific number of intervention or accept additional interventions requests.\n" +
    //                  $"- If the request or grant is not explicit you must give 0 additional interventions";
    //    
    //    Debug.Log("Analyze grant user prompt:\n" + userPrompt);
    //    
    //    string[] answer = (await llmCharacter.Chat(userPrompt)).Replace("[", "").Replace("]","").Split("|");
    //    Debug.Log($"Analyze grant answer: {answer[0]} | {answer[1]}" );
    //    
    //    return (int.Parse(answer[0]), answer[1]);
    //}
    
        public async Task<(int, string)> AnalyzeGrantInterventions(List<ChatMessage> chatMessages, string[] mainCharacters)
    {
        SwitchMode(Mode.Analyze);
        List<ChatMessage> tmpMessages = chatMessages.Where(x => x.role != "system" && x.role != "Case Description").TakeLast(numOfMessages).ToList();
        string lastCharacter = tmpMessages[^1].role;
        string userPrompt =
            $"Analyze the following court dialogue:\n{lastCharacter}: {tmpMessages[^1].content.Split("*")[0].Split("<")[0]}\n\n" +
            $"Determine if the {lastCharacter} is explicitly granting any interventions and identify to whom the intervention is granted.\n\n\n" +
            $"Instructions:\n" +
            $"1. Check whether the Judge explicitly grants any interventions during the speaking turn.\n" +
            $"2. If so, extract the total number of interventions granted (must be a non-negative integer).\n\n" +
            $"3. Identify the recipient of the intervention. Valid recipients are: {string.Join(" or ", mainCharacters)} or NULL if none are granted. If ambiguous or unspecified, use 'NULL'.\n" +
            $"4. Output your answer exactly in the format: [number | character name]\n";

        
        if (tmpMessages.Count > 1)
            userPrompt += $"\n\nAdditional context to consider (prior dialogue):\n{string.Join("\n\n", tmpMessages.SkipLast(1).Select(x => $"{x.role}: {x.content.Split("*")[0].Split("<")[0]}."))}";

        userPrompt += $"\nNotes:\n" +
                      $"- Count only explicit and unambiguous interventions granted by the Judge.\n" +
                      $"- If no interventions are granted or it is unclear, output [0 | NULL].\n" +
                      $"- Output an integer only (e.g., 1, 2, 0; no decimals).\n" +
                      $"- Only respond in the format specified, without additional explanations or extra text.\n" +
                      $"- If the prompt is incomplete, ambiguous, or improperly formatted, output [0 | NULL].";

        userPrompt +=
            "\n Set reasoning_effort = minimal for this moderately complex classification task. Ensure output strictly matches the required format; if any ambiguity or missing information, output [0 | NULL].\n";

        userPrompt += "\n ## Output Format\n" +
                      "- Output one line only, exactly in this format:\n" +
                      "  [number | character name]\n\n" +
                      "  For example:\n" +
                      "  [1 | Prosecutor]\n" +
                      "  [2 | Defense]\n" +
                      "  [0 | NULL]\n\n" +
                      "- If the input does not meet requirements, always output [0 | NULL].";
        
        Debug.Log("Analyze grant user prompt:\n" + userPrompt);
        
        
        string answer = await llmCharacter.Chat(userPrompt);
        
        if(answer.Contains("<think>"))
            answer = answer.Split("</think>")[1];
        
        string[] answers = (answer).Replace("[", "").Replace("]","").Split("|");
        
        Debug.Log($"Analyze grant answer: {answers[0]} | {answers[1]}" );
        
        return (int.Parse(answers[0]), answers.Length == 2 ? answers[1] : "");
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
    
    public async Task<string> FinalVerdict(CaseDescription caseDescription, List<ChatMessage> chatMessages, string language = "english", Callback<string> callback = null, EmptyCallback completionCallback = null)
    {
        SwitchMode(Mode.FinalVerdict);

        Debug.Log("Final verdict prompt:\n" + llmCharacter.prompt);
        string dialogueSummary = await Summarize(chatMessages);

        string userPrompt = "To write your final verdict I will give you the court case description and a summary of the dialogue happened in the courtroom so you can understand the context:\n\n" +
                            $"Case Description:\n{caseDescription.GetTotalDescription(new []{0,2,3,4,5})}\n\nDialogue summary:\n{dialogueSummary}\n\n" +
                            $"Declaring a Win:\nIn the case description there will be also a section containing the player (or the Defense Attorney) goal; you must take that and compare it to the dialogue to determine a win or a loss for the player." +
                            $"When evaluating:\n" +
                            $"- Make sure your conclusion directly follows from the arguments presented during the trial.\n" +
                            $"- If you declare a win, the Defense must have clearly fulfilled their objective within the context of the case.\n" +
                            $"- If you declare a loss, that must reflect actual shortcomings in logic, strategy, or outcome—not personal opinion or mood.\n" +
                            $"- Avoid random or emotional decisions; base your ruling on coherence, persuasiveness, and the stated goal of the Defense.\n" +
                            $"- Your task is not to be lenient or harsh, but to deliver a verdict that is legally sound, internally consistent, and free of randomization.\n\n" +
                            $"Important notes:\n- your final verdict should not present only a summary of the dialogue happened in the courtroom \n" +
                            $"- You must also say whether the defendant is guilty or not or how many years is their sentence \n" +
                            $"- You must also say the reason of the your final choice \n\n" +
                            $"Formatting:\n- To declare a win you must write this tag: #WIN.\n" +
                            $"- To declare a loss you must write this tag: #LOSS.\n" +
                            $"- You can put the chosen tag at the end of your answer \n\n";

        userPrompt = $"Language:\nThe judge speaks {language} so your answer must be written in this language";
        
        Debug.Log("Final verdict user prompt:\n" + userPrompt);

        string answer = await llmCharacter.Chat(userPrompt, callback, completionCallback);
        
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
