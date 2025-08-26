using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Kokoro;
using Microsoft.ML.OnnxRuntime.Unity;
using UnityEngine;

public class KokoroTTSManager : MonoBehaviour
{
    [Header("Kokoro TTS Configuration")]
    [SerializeField] private RemoteFile modelUrl = new("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/onnx/model_fp16.onnx");
    [SerializeField] private TextToSpeechOptions options;
    [SerializeField] private LanguageCode language = LanguageCode.En_US;

    [Header("Voice Lists")]
    [SerializeField] private string[] allVoices;
    [SerializeField] private string[] maleVoiceNames;
    [SerializeField] private string[] femaleVoiceNames;

    [Header("Audio Settings")]
    [SerializeField, Range(0.1f, 3f)] private float judgeSpeed = 0.9f;
    [SerializeField, Range(0.1f, 3f)] private float prosecutorSpeed = 1.1f;
    [SerializeField, Range(0.1f, 3f)] private float witnessSpeed = 1.0f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("TTS Streaming Settings")]
    [SerializeField] private float sentenceBufferDelay = 0.2f;
    [SerializeField] private int minWordsForTTS = 4;
    [SerializeField] private float silenceBetweenSentences = 0.3f;

    [Header("Judge Voice Settings")]
    [SerializeField] private string[] judgeVoices = { "am_michael", "am_santa" };

    private KokoroTTS tts;
    private Dictionary<string, string> characterToVoice;
    private Dictionary<string, string> characterGenders;

    private AudioSource currentAudioSource;
    private string currentSpeakingCharacter = "";
    private string currentActiveVoice = "";

    public bool isInitialized { get; private set; } = false;
    private HashSet<string> loadedVoices = new HashSet<string>();

    private TTSStreamingState streamingState;

    private struct TTSStreamingState
    {
        public string character;
        public string accumulatedText;
        public string lastProcessedText;
        public bool isStreaming;
        public CancellationTokenSource cancellationToken;
        public Queue<SentenceData> pendingSentences;
        public bool isProcessingQueue;
        public int processedSentenceCount;
        public HashSet<string> processedSentencesHash;

        public void Initialize(string characterName)
        {
            character = characterName;
            accumulatedText = "";
            lastProcessedText = "";
            isStreaming = true;
            cancellationToken?.Dispose();
            cancellationToken = new CancellationTokenSource();
            pendingSentences = new Queue<SentenceData>();
            isProcessingQueue = false;
            processedSentenceCount = 0;
            processedSentencesHash = new HashSet<string>();
        }

        public void Clear()
        {
            isStreaming = false;
            isProcessingQueue = false;

            if (cancellationToken != null && !cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    cancellationToken.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            cancellationToken?.Dispose();
            cancellationToken = null;
            pendingSentences?.Clear();
            processedSentencesHash?.Clear();
            accumulatedText = "";
            lastProcessedText = "";
            character = "";
            processedSentenceCount = 0;
        }
    }

    private struct SentenceData
    {
        public string text;
        public int sentenceIndex;
        public string voiceName;

        public SentenceData(string text, int index, string voice)
        {
            this.text = text;
            this.sentenceIndex = index;
            this.voiceName = voice;
        }
    }

    public static KokoroTTSManager Instance { get; private set; }
    public event System.Action OnTTSReady;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("TTS Manager created and set as DontDestroyOnLoad");
        }
        else if (Instance != this)
        {
            Debug.Log("TTS Manager already exists, destroying duplicate");
            Destroy(gameObject);
            return;
        }
        Debug.Log("[KokoroTTSManager] Awake chiamato su " + gameObject.name);
        Debug.Log("TTS Manager Awake completed");
    }

    private async void Start()
    {
        if (Instance == this && !isInitialized)
        {
            try
            {
                await InitializeTTS();
            }
            catch (Exception e)
            {
                Debug.LogError($"TTS Manager initialization failed: {e.Message}");
            }
        }
    }

    private async System.Threading.Tasks.Task InitializeTTS()
    {
        if (isInitialized)
        {
            Debug.Log("TTS already initialized, skipping...");
            OnTTSReady?.Invoke();
            return;
        }

        try
        {
            Debug.Log("Initializing Kokoro TTS...");

            CancellationToken cancellationToken = destroyCancellationToken;
            byte[] modelData = await modelUrl.Load(cancellationToken);
            await Awaitable.MainThreadAsync();
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            tts = new KokoroTTS(modelData, options);
            await tts.InitializeLanguageAsync(language, cancellationToken);

            InitializeBasicStructures();
            CreateSingleAudioSource();

            isInitialized = true;
            Debug.Log("Kokoro TTS initialized successfully");

            OnTTSReady?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Kokoro TTS: {e.Message}");
        }
    }

    private void InitializeBasicStructures()
    {
        if (characterToVoice == null) characterToVoice = new Dictionary<string, string>();
        if (characterGenders == null) characterGenders = new Dictionary<string, string>();

        char langPrefix = KokoroTTS.GetLanguagePrefix(language);
        var supportedMaleVoices = maleVoiceNames.Where(voice => voice[0] == langPrefix).ToArray();
        var supportedFemaleVoices = femaleVoiceNames.Where(voice => voice[0] == langPrefix).ToArray();

        Debug.Log($"Available male voices: {string.Join(", ", supportedMaleVoices)}");
        Debug.Log($"Available female voices: {string.Join(", ", supportedFemaleVoices)}");
    }

    private void CreateSingleAudioSource()
    {
        if (currentAudioSource == null)
        {
            GameObject audioGO = new GameObject("TTS_AudioSource");
            audioGO.transform.SetParent(transform);

            currentAudioSource = audioGO.AddComponent<AudioSource>();
            currentAudioSource.volume = volume;
            currentAudioSource.playOnAwake = false;
            currentAudioSource.spatialBlend = 0f;
        }
    }

    public void InitializeCharacterVoices(List<string> witnessNames, List<string> witnessGenders, string prosecutorGender)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("TTS not initialized yet, cannot assign voices");
            return;
        }

        if (tts == null || witnessNames == null || witnessGenders == null)
        {
            Debug.LogError("Invalid data for voice initialization");
            return;
        }

        // Reset completo del sistema voci
        characterToVoice.Clear();
        characterGenders.Clear();
        loadedVoices.Clear();
        currentActiveVoice = "";

        char langPrefix = KokoroTTS.GetLanguagePrefix(language);
        var supportedMaleVoices = maleVoiceNames.Where(voice => voice[0] == langPrefix).ToList();
        var supportedFemaleVoices = femaleVoiceNames.Where(voice => voice[0] == langPrefix).ToArray();

        AssignJudgeVoice();

        var availableMaleVoices = supportedMaleVoices.Where(v => !judgeVoices.Contains(v)).ToList();

        List<string> prosecutorVoices = prosecutorGender == "M" ? availableMaleVoices : supportedFemaleVoices.ToList();
        AssignVoiceToCharacter("Prosecutor", prosecutorGender, prosecutorVoices);

        for (int i = 0; i < witnessNames.Count; i++)
        {
            string witnessName = witnessNames[i];
            string gender = i < witnessGenders.Count ? witnessGenders[i].Trim().ToUpper() : "M";
            List<string> witnessVoices = gender == "M" ? availableMaleVoices : supportedFemaleVoices.ToList();
            AssignVoiceToCharacter(witnessName, gender, witnessVoices);
        }

        Debug.Log("Voice assignments completed:");
        foreach (var kvp in characterToVoice)
        {
            Debug.Log($"- {kvp.Key} ({characterGenders[kvp.Key]}): {kvp.Value}");
        }
    }

    private void AssignJudgeVoice()
    {
        char langPrefix = KokoroTTS.GetLanguagePrefix(language);
        var availableJudgeVoices = judgeVoices.Where(voice => voice[0] == langPrefix).ToList();

        if (availableJudgeVoices.Count > 0)
        {
            string selectedVoice = availableJudgeVoices[UnityEngine.Random.Range(0, availableJudgeVoices.Count)];
            characterToVoice["Judge"] = selectedVoice;
            characterGenders["Judge"] = "M";
            Debug.Log($"Assigned specific Judge voice: {selectedVoice}");
        }
        else
        {
            Debug.LogWarning("No specific judge voices available, using default male voice");
            var fallbackVoices = maleVoiceNames.Where(voice => voice[0] == langPrefix).ToList();
            if (fallbackVoices.Count > 0)
            {
                string selectedVoice = fallbackVoices[UnityEngine.Random.Range(0, fallbackVoices.Count)];
                characterToVoice["Judge"] = selectedVoice;
                characterGenders["Judge"] = "M";
            }
        }
    }

    private void AssignVoiceToCharacter(string characterName, string gender, List<string> availableVoices)
    {
        if (availableVoices.Count == 0)
        {
            Debug.LogWarning($"No voices available for {characterName} ({gender})");
            return;
        }

        string selectedVoice = availableVoices[UnityEngine.Random.Range(0, availableVoices.Count)];
        characterToVoice[characterName] = selectedVoice;
        characterGenders[characterName] = gender;

        Debug.Log($"Assigned voice '{selectedVoice}' to {characterName} ({gender})");
    }

    public void StartStreamingTTS(string characterName)
    {
        if (!isInitialized || characterName.ToLower().Contains("defense"))
        {
            return;
        }

        StopAllSpeech();
        streamingState.Initialize(characterName);
        currentSpeakingCharacter = characterName;

        Debug.Log($"Started TTS streaming for: {characterName}");
    }

    public void UpdateStreamingText(string characterName, string currentText)
    {
        if (!streamingState.isStreaming || streamingState.character != characterName)
        {
            return;
        }

        string cleanText = CleanAndValidateText(currentText);
        streamingState.accumulatedText = cleanText;

        ProcessNewSentences();
    }

    public void FinalizeStreamingTTS(string characterName, Action onComplete = null)
    {
        if (!streamingState.isStreaming || streamingState.character != characterName)
        {
            onComplete?.Invoke();
            return;
        }

        streamingState.isStreaming = false;

        // Prendi tutto il testo residuo, anche se corto
        string remainingText = GetRemainingText();
        if (!string.IsNullOrWhiteSpace(remainingText))
        {
            string cleanRemaining = remainingText.Trim();
            string voiceName = characterToVoice.GetValueOrDefault(characterName, "");
            string sentenceHash = cleanRemaining.ToLower();

            if (!streamingState.processedSentencesHash.Contains(sentenceHash))
            {
                int nextIndex = streamingState.processedSentenceCount;
                streamingState.pendingSentences.Enqueue(new SentenceData(cleanRemaining, nextIndex, voiceName));
                streamingState.processedSentenceCount++;
                streamingState.processedSentencesHash.Add(sentenceHash);
            }
        }

        if (!streamingState.isProcessingQueue)
        {
            _ = ProcessTTSQueue(onComplete);
        }

        Debug.Log($"Finalized TTS streaming for: {characterName}");
    }

    private void ProcessNewSentences()
    {
        string newText = GetRemainingText();
        var newSentences = ExtractReadyText(newText);

        foreach (string sentence in newSentences)
        {
            string cleanSentence = sentence.Trim();
            if (CountWords(cleanSentence) >= minWordsForTTS)
            {
                string voiceName = characterToVoice.GetValueOrDefault(streamingState.character, "");
                string sentenceHash = cleanSentence.ToLower();

                if (!streamingState.processedSentencesHash.Contains(sentenceHash))
                {
                    int sentenceIndex = streamingState.processedSentenceCount;
                    streamingState.pendingSentences.Enqueue(new SentenceData(cleanSentence, sentenceIndex, voiceName));
                    streamingState.processedSentenceCount++;
                    streamingState.processedSentencesHash.Add(sentenceHash);

                    // Aggiorna il testo processato
                    int sentenceEndPos = streamingState.accumulatedText.IndexOf(sentence) + sentence.Length;
                    if (sentenceEndPos <= streamingState.accumulatedText.Length)
                    {
                        streamingState.lastProcessedText = streamingState.accumulatedText.Substring(0, sentenceEndPos);
                    }
                }
            }
        }

        if (streamingState.pendingSentences.Count > 0 && !streamingState.isProcessingQueue)
        {
            _ = ProcessTTSQueue();
        }
    }

    private string GetRemainingText()
    {
        if (string.IsNullOrEmpty(streamingState.lastProcessedText))
        {
            return streamingState.accumulatedText;
        }

        int lastProcessedLength = streamingState.lastProcessedText.Length;
        if (lastProcessedLength >= streamingState.accumulatedText.Length)
        {
            return "";
        }

        return streamingState.accumulatedText.Substring(lastProcessedLength);
    }

    private List<string> ExtractReadyText(string text)
    {
        List<string> readySegments = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return readySegments;

        char[] delimiters = new char[] { '.', '!', '?', ';', ':' };
        int lastEnd = 0;

        //readySegments = text.Split('.',StringSplitOptions.RemoveEmptyEntries).ToList();
        //Debug.Log("Text segments: \n" + string.Join("\n",readySegments));
        
        for (int i = 0; i < text.Length; i++)
        {
            if (delimiters.Contains(text[i]))
            {
                // Controlli per abbreviazioni e decimali
                if (i > 0 && i < text.Length - 1)
                {
                    char prev = text[i - 1];
                    char next = text[i + 1];
        
                    // Ignora punto dopo abbreviazione tipo "Dr." o "Mr."
                    if (text[i] == '.' && char.IsUpper(next) == false && char.IsLetter(prev))
                        continue;
        
                    // Ignora decimali
                    if (text[i] == '.' && char.IsDigit(prev) && char.IsDigit(next))
                        continue;
                }
        
                string sentence = text.Substring(lastEnd, (i - lastEnd) + 1).Trim();
                if (!string.IsNullOrEmpty(sentence))
                {
                    readySegments.Add(sentence);
                    lastEnd = i + 1;
                }
            }
        }
        
        // Gestione del testo residuo (ultima frase)
        if (lastEnd < text.Length)
        {
            string remaining = text.Substring(lastEnd).Trim();
            if (!string.IsNullOrEmpty(remaining))
            {
                readySegments.Add(remaining); // forza sempre la generazione dell'ultima frase
            }
        }
        
        // Eventuale accodamento della frase corta all'ultima frase precedente
        for (int j = 0; j < readySegments.Count; j++)
        {
            if (CountWords(readySegments[j]) < minWordsForTTS && j > 0)
            {
                readySegments[j - 1] += " " + readySegments[j];
                readySegments[j] = null;
            }
        }

        return readySegments.Where(s => s != null).ToList();
    }



    private int CountWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Split(new char[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private async System.Threading.Tasks.Task ProcessTTSQueue(Action finalCallback = null)
    {
        if (streamingState.isProcessingQueue) return;

        streamingState.isProcessingQueue = true;
        var currentToken = streamingState.cancellationToken;

        try
        {
            while ((streamingState.pendingSentences.Count > 0 || streamingState.isStreaming)
                   && currentToken != null && !currentToken.Token.IsCancellationRequested)
            {
                if (streamingState.pendingSentences.Count == 0 && streamingState.isStreaming)
                {
                    await System.Threading.Tasks.Task.Delay((int)(sentenceBufferDelay * 1000), currentToken.Token);
                    continue;
                }

                if (streamingState.pendingSentences.Count == 0) break;

                var sentenceData = streamingState.pendingSentences.Dequeue();

                // Genera la voce con la voce specifica salvata nella frase
                await GenerateSpeechInternal(sentenceData.text, streamingState.character, sentenceData.voiceName, currentToken.Token);

                if (currentToken.Token.IsCancellationRequested) break;

                await WaitForAudioCompletion(currentToken.Token);

                if (!currentToken.Token.IsCancellationRequested && streamingState.pendingSentences.Count > 0)
                {
                    await System.Threading.Tasks.Task.Delay((int)(silenceBetweenSentences * 1000), currentToken.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("TTS processing cancelled gracefully");
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("TTS processing stopped due to disposed token");
        }
        finally
        {
            streamingState.isProcessingQueue = false;
            if (!streamingState.isStreaming)
            {
                currentSpeakingCharacter = "";
                finalCallback?.Invoke();
            }
        }
    }

    private async System.Threading.Tasks.Task GenerateSpeechInternal(string text, string characterName, string voiceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(voiceName))
        {
            Debug.LogWarning($"No voice name provided for character: {characterName}");
            return;
        }

        try
        {
            if (currentActiveVoice != voiceName)
            {
                string voiceUrl = "file://" + Path.Combine(Application.streamingAssetsPath, "Voices", $"{voiceName}.bin");
                await tts.LoadVoiceAsync(new Uri(voiceUrl), cancellationToken);
                currentActiveVoice = voiceName;
                loadedVoices.Add(voiceName);
                Debug.Log($"SWITCHED to voice: {voiceName} for character: {characterName}");
            }

            float speed = GetCharacterSpeed(characterName);
            tts.Speed = speed;

            string cleanedText = CleanAndValidateText(text);
            if (string.IsNullOrEmpty(cleanedText)) return;

            var audioClip = await tts.GenerateAudioClipAsync(cleanedText, cancellationToken);

            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (currentAudioSource.isPlaying)
                currentAudioSource.Stop();

            if (currentAudioSource.clip != null)
                Destroy(currentAudioSource.clip);

            currentAudioSource.clip = audioClip;
            currentAudioSource.Play();

            Debug.Log($"Playing TTS for {characterName} ({voiceName}): {cleanedText.Substring(0, Math.Min(30, cleanedText.Length))}...");
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"TTS generation cancelled for {characterName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"TTS generation failed for {characterName}: {e.Message}");
        }
    }

    private string CleanAndValidateText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // CORREZIONE: Pulizia più delicata per preservare i nomi propri
        string cleaned = text;

        // Rimuovi solo tag HTML/markdown evidenti
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "<[^>]*>", "");
        cleaned = cleaned.Replace("*", "").Replace("#", "");

        // Normalizza spazi multipli ma preserva la struttura
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");

        // Rimuovi caratteri di controllo problematici
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[\x00-\x1F\x7F]", "");

        cleaned = cleaned.Trim();

        // Assicura punteggiatura finale solo se necessario
        if (cleaned.Length > 0 && !".!?;:,".Contains(cleaned[cleaned.Length - 1]))
        {
            cleaned += ".";
        }

        return cleaned;
    }

    private async System.Threading.Tasks.Task WaitForAudioCompletion(CancellationToken cancellationToken = default)
    {
        try
        {
            while (currentAudioSource != null && currentAudioSource.isPlaying && !cancellationToken.IsCancellationRequested)
            {
                await Awaitable.NextFrameAsync();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Audio completion wait cancelled");
        }
    }

    private float GetCharacterSpeed(string characterName)
    {
        if (characterName == "Judge") return judgeSpeed;
        if (characterName == "Prosecutor") return prosecutorSpeed;
        return witnessSpeed;
    }
    /*public void GenerateSpeech(string characterName, string text, Action onComplete = null)
    {
        if (!isInitialized || characterName.ToLower().Contains("defense"))
        {
            onComplete?.Invoke();
            return;
        }

        StartStreamingTTS(characterName);
        UpdateStreamingText(characterName, text);
        FinalizeStreamingTTS(characterName, onComplete);
    }*/

    /*public string GetCharacterGender(string characterName)
    {
        return characterGenders.TryGetValue(characterName, out string gender) ? gender : "M";
    }*/

    public void StopAllSpeech()
    {
        Debug.Log("Stopping all TTS speech...");

        if (currentAudioSource != null && currentAudioSource.isPlaying)
        {
            currentAudioSource.Stop();
        }

        streamingState.Clear();
        currentSpeakingCharacter = "";

        Debug.Log("TTS speech stopped");
    }

    /*public bool IsAnySpeaking()
    {
        return currentAudioSource != null && currentAudioSource.isPlaying;
    }

    public bool IsCharacterSpeaking(string characterName)
    {
        return currentSpeakingCharacter == characterName && IsAnySpeaking();
    }*/

    private void OnDestroy()
    {
        Debug.Log("TTS Manager destroying...");
        StopAllSpeech();

        if (tts != null)
        {
            try
            {
                tts.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error disposing TTS: {e.Message}");
            }
        }

        Debug.Log("TTS Manager destroyed");
    }

    public void ResetForNewGame()
    {
        StopAllSpeech();
        currentSpeakingCharacter = "";
        currentActiveVoice = "";

        if (characterToVoice != null) characterToVoice.Clear();
        if (characterGenders != null) characterGenders.Clear();
        if (loadedVoices != null) loadedVoices.Clear();

        CreateSingleAudioSource();
    }

    [ContextMenu("Update Voice List")]
    private void UpdateVoiceList()
    {
        string dir = Path.Combine(Application.streamingAssetsPath, "Voices");
        if (!Directory.Exists(dir))
        {
            Debug.LogWarning($"Voice directory does not exist: {dir}");
            allVoices = Array.Empty<string>();
            return;
        }
        allVoices = Directory.GetFiles(dir, "*.bin")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();

        Debug.Log($"Found {allVoices.Length} voice files");
    }
}