using System;
using System.IO;
using System.Linq;
using System.Threading;
using Kokoro;
using Microsoft.ML.OnnxRuntime.Unity;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
sealed class KokoroTTSDemo : MonoBehaviour
{
    [SerializeField]
    RemoteFile modelUrl = new("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/onnx/model_fp16.onnx");

    [SerializeField]
    TextToSpeechOptions options;

    [SerializeField]
    LanguageCode language = LanguageCode.En_US;

    [SerializeField]
    [ContextMenuItem("Update Voice List", nameof(UpdateVoiceList))]
    string[] allVoices;

    [SerializeField]
    string speechText = "Life is like a box of chocolates. You never know what you're gonna get.";

    [SerializeField]
    [Range(0.1f, 5f)]
    float speechSpeed = 1f;

    [Header("UI References")]
    [SerializeField] private InputField inputField;
    [SerializeField] private Dropdown voicesDropdown;
    [SerializeField] private Button speechButton;

    KokoroTTS tts;
    AudioSource audioSource;

    async void Start()
    {
        Application.runInBackground = true;
        audioSource = GetComponent<AudioSource>();

        // Setup Kokoro
        Debug.Log("Loading model...");
        CancellationToken cancellationToken = destroyCancellationToken;
        byte[] modelData = await modelUrl.Load(cancellationToken);
        await Awaitable.MainThreadAsync();
        cancellationToken.ThrowIfCancellationRequested();

        tts = new KokoroTTS(modelData, options);
        await tts.InitializeLanguageAsync(language, cancellationToken);
        Debug.Log("TTS created");

        // UI Setup
        // Filter voices
        char selectedLangPrefix = KokoroTTS.GetLanguagePrefix(language);
        var filteredVoices = allVoices.Where(voice => voice[0] == selectedLangPrefix).ToList();

        voicesDropdown.ClearOptions();
        voicesDropdown.AddOptions(filteredVoices);

        // Input field inizializzato con testo
        inputField.text = speechText;
        inputField.onValueChanged.AddListener(value => speechText = value);

        // Dropdown selection
        voicesDropdown.onValueChanged.AddListener(async index =>
        {
            await LoadVoiceAsync(index);
            Debug.Log($"Selected voice: {filteredVoices[index]}");
        });
        voicesDropdown.value = 0;
        await LoadVoiceAsync(0);

        // Button
        speechButton.onClick.AddListener(async () => await GenerateAsync());
    }

    void OnDestroy()
    {
        tts?.Dispose();
    }

    async Awaitable LoadVoiceAsync(int index)
    {
        string url = "file://" + Path.Combine(Application.streamingAssetsPath, "Voices", $"{allVoices[index]}.bin");
        await tts.LoadVoiceAsync(new Uri(url), destroyCancellationToken);
    }

    async Awaitable GenerateAsync()
    {
        Debug.Log($"Generating speech: {speechText}");
        tts.Speed = speechSpeed;
        var clip = await tts.GenerateAudioClipAsync(speechText, destroyCancellationToken);
        Debug.Log("Speech generated");

        if (audioSource.clip != null)
        {
            Destroy(audioSource.clip);
        }
        audioSource.clip = clip;
        audioSource.Play();
    }

    [ContextMenu("Update Voice List")]
    void UpdateVoiceList()
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
    }
}
