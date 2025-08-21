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

    private KokoroTTS tts;
    private Dictionary<string, string> characterToVoice;
    private Dictionary<string, AudioSource> characterAudioSources;
    private Dictionary<string, string> characterGenders; // Track character genders
    public bool isInitialized { get; private set; } = false;

    private Queue<TTSRequest> ttsQueue = new Queue<TTSRequest>();
    private bool isProcessing = false;
    private HashSet<string> loadedVoices = new HashSet<string>();

    private struct TTSRequest
    {
        public string character;
        public string text;
        public Action onComplete;
        public CancellationToken cancellationToken;
    }

    public static KokoroTTSManager Instance { get; private set; }

    public event System.Action OnTTSReady;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
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

    private async System.Threading.Tasks.Task InitializeTTS()
    {
        try
        {
            Debug.Log("Initializing Kokoro TTS...");

            CancellationToken cancellationToken = destroyCancellationToken;
            byte[] modelData = await modelUrl.Load(cancellationToken);
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            tts = new KokoroTTS(modelData, options);
            await tts.InitializeLanguageAsync(language, cancellationToken);

            InitializeBasicStructures();

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
        characterToVoice = new Dictionary<string, string>();
        characterAudioSources = new Dictionary<string, AudioSource>();
        characterGenders = new Dictionary<string, string>();

        char langPrefix = KokoroTTS.GetLanguagePrefix(language);
        var supportedMaleVoices = maleVoiceNames.Where(voice => voice[0] == langPrefix).ToArray();
        var supportedFemaleVoices = femaleVoiceNames.Where(voice => voice[0] == langPrefix).ToArray();

        Debug.Log($"Available male voices: {string.Join(", ", supportedMaleVoices)}");
        Debug.Log($"Available female voices: {string.Join(", ", supportedFemaleVoices)}");
    }

    /// <summary>
    /// Inizializza le voci per tutti i personaggi del caso
    /// </summary>
    public void InitializeCharacterVoices(List<string> witnessNames, List<string> witnessGenders, string prosecutorGender)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("TTS not initialized yet, cannot assign voices");
            return;
        }

        if (tts == null)
        {
            Debug.LogError("TTS instance is null");
            return;
        }

        if (witnessNames == null || witnessGenders == null)
        {
            Debug.LogError("Invalid witness data provided");
            return;
        }

        char langPrefix = KokoroTTS.GetLanguagePrefix(language);
        var supportedMaleVoices = maleVoiceNames.Where(voice => voice[0] == langPrefix).ToList();
        var supportedFemaleVoices = femaleVoiceNames.Where(voice => voice[0] == langPrefix).ToList();

        AssignVoiceToCharacter("Judge", "M", supportedMaleVoices, judgeSpeed);

        List<string> prosecutorVoices = prosecutorGender == "M" ? supportedMaleVoices : supportedFemaleVoices;
        AssignVoiceToCharacter("Prosecutor", prosecutorGender, prosecutorVoices, prosecutorSpeed);

        for (int i = 0; i < witnessNames.Count; i++)
        {
            string witnessName = witnessNames[i];
            string gender = i < witnessGenders.Count ? witnessGenders[i].Trim().ToUpper() : "M";

            List<string> witnessVoices = gender == "M" ? supportedMaleVoices : supportedFemaleVoices;
            AssignVoiceToCharacter(witnessName, gender, witnessVoices, witnessSpeed);
        }

        Debug.Log("Voice assignments completed:");
        foreach (var kvp in characterToVoice)
        {
            Debug.Log($"- {kvp.Key} ({characterGenders[kvp.Key]}): {kvp.Value}");
        }
    }

    private void AssignVoiceToCharacter(string characterName, string gender, List<string> availableVoices, float speed)
    {
        if (availableVoices.Count == 0)
        {
            Debug.LogWarning($"No voices available for {characterName} ({gender})");
            return;
        }

        // Random voice selection
        string selectedVoice = availableVoices[UnityEngine.Random.Range(0, availableVoices.Count)];

        characterToVoice[characterName] = selectedVoice;
        characterGenders[characterName] = gender;

        CreateAudioSourceForCharacter(characterName, speed);

        Debug.Log($"Assigned voice '{selectedVoice}' to {characterName} ({gender})");
    }

    private void CreateAudioSourceForCharacter(string characterName, float speed)
    {
        GameObject audioGO = new GameObject($"AudioSource_{characterName}");
        audioGO.transform.SetParent(transform);

        AudioSource audioSource = audioGO.AddComponent<AudioSource>();
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound

        characterAudioSources[characterName] = audioSource;
    }

    /// <summary>
    /// Genera speech per un personaggio, spezzando il testo
    /// </summary>
    private static readonly char[] SentenceDelimiters = { '.', '!', '?', ';', ':' };

    public void GenerateSpeech(string characterName, string text, Action onComplete = null)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("TTS not initialized yet");
            onComplete?.Invoke();
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            onComplete?.Invoke();
            return;
        }

        if (characterName.ToLower().Contains("defense"))
        {
            onComplete?.Invoke();
            return;
        }

        // Prendi solo frasi intere
        var sentences = SplitIntoSentences(CleanTextForTTS(text));

        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i].Trim();
            if (string.IsNullOrEmpty(sentence)) continue;

            var request = new TTSRequest
            {
                character = characterName,
                text = sentence,
                onComplete = (i == sentences.Count - 1) ? onComplete : null, // callback solo all’ultima frase
                cancellationToken = destroyCancellationToken
            };

            ttsQueue.Enqueue(request);
        }

        if (!isProcessing)
            _ = ProcessTTSQueue();
    }

    private List<string> SplitIntoSentences(string text)
    {
        List<string> sentences = new List<string>();
        int start = 0;

        
        for (int i = 0; i < text.Length; i++)
        {
            if (SentenceDelimiters.Contains(text[i]))
            {
                int length = (i - start) + 1;
                string sentence = text.Substring(start, length).Trim();
                if (!string.IsNullOrEmpty(sentence))
                    sentences.Add(sentence);

                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            string lastSentence = text.Substring(start).Trim();
            if (!string.IsNullOrEmpty(lastSentence))
                sentences.Add(lastSentence);
        }

        return sentences;
    }


    private async System.Threading.Tasks.Task ProcessTTSQueue()
    {
        isProcessing = true;

        while (ttsQueue.Count > 0)
        {
            var request = ttsQueue.Dequeue();

            try
            {
                await GenerateSpeechInternal(request);
            }
            catch (Exception e)
            {
                Debug.LogError($"TTS generation failed for {request.character}: {e.Message}");
                request.onComplete?.Invoke();
            }
        }

        isProcessing = false;
    }

    private async System.Threading.Tasks.Task GenerateSpeechInternal(TTSRequest request)
    {
        if (!characterToVoice.TryGetValue(request.character, out string voiceName))
        {
            Debug.LogWarning($"No voice assigned for character: {request.character}");
            request.onComplete?.Invoke();
            return;
        }

        //Carica la voce solo se non è già stata caricata
        if (!loadedVoices.Contains(voiceName))
        {
            string voiceUrl = "file://" + Path.Combine(Application.streamingAssetsPath, "Voices", $"{voiceName}.bin");
            await tts.LoadVoiceAsync(new Uri(voiceUrl), request.cancellationToken);
            loadedVoices.Add(voiceName);
        }

        float speed = GetCharacterSpeed(request.character);
        tts.Speed = speed;

        var audioClip = await tts.GenerateAudioClipAsync(request.text, request.cancellationToken);

        await Awaitable.MainThreadAsync();

        if (characterAudioSources.TryGetValue(request.character, out AudioSource audioSource))
        {
            if (audioSource.clip != null)
                Destroy(audioSource.clip);

            audioSource.clip = audioClip;
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning($"No AudioSource found for character: {request.character}");
            request.onComplete?.Invoke();
        }
    }

    private float GetCharacterSpeed(string characterName)
    {
        if (characterName == "Judge") return judgeSpeed;
        if (characterName == "Prosecutor") return prosecutorSpeed;
        return witnessSpeed; // Default for witnesses
    }

    private string CleanTextForTTS(string text)
    {
        string cleanText = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
        cleanText = cleanText.Replace("<", "").Replace(">", "")
                           .Replace("[", "").Replace("]", "")
                           .Replace("*", "").Replace("#", "");
        cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"\s+", " ");
        return cleanText.Trim();
    }

    private async System.Threading.Tasks.Task WaitForAudioCompletion(AudioSource audioSource, Action onComplete)
    {
        while (audioSource != null && audioSource.isPlaying)
        {
            await Awaitable.NextFrameAsync();
        }
        //await Awaitable.WaitForSecondsAsync(1.0f);
        onComplete?.Invoke();
    }

    public string GetCharacterGender(string characterName)
    {
        return characterGenders.TryGetValue(characterName, out string gender) ? gender : "M";
    }

    public void StopAllSpeech()
    {
        foreach (var audioSource in characterAudioSources.Values)
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }
        ttsQueue.Clear();
        isProcessing = false;
    }

    public void StopCharacterSpeech(string characterName)
    {
        if (characterAudioSources.TryGetValue(characterName, out AudioSource audioSource))
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }
    }

    public bool IsAnySpeaking()
    {
        return characterAudioSources.Values.Any(audio => audio != null && audio.isPlaying);
    }

    public bool IsCharacterSpeaking(string characterName)
    {
        return characterAudioSources.TryGetValue(characterName, out AudioSource audioSource) &&
               audioSource != null && audioSource.isPlaying;
    }

    private void OnDestroy()
    {
        tts?.Dispose();
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