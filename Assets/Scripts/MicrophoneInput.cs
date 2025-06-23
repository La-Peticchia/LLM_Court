using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

public class MicrophoneInput : MonoBehaviour
{
    [Header("Dependencies")]
    public WhisperManager whisper;
    public MicrophoneRecord microphoneRecord;

    [Header("UI")]
    public Button micButton;
    public Text buttonText;
    public Image micIcon;
    public Sprite micIdleSprite;
    public Sprite micRecSprite; 
    public InputField playerInput;        
    public Court court;                   // per sapere quando può parlare

    private bool isRecording = false;


    void Awake()
    {
        micButton.onClick.AddListener(ToggleRecording);
        microphoneRecord.OnRecordStop += OnRecordStop;

        // Imposta lingua automatica (lingua vuota o null, dipende da whisper)
        whisper.language = "";

        // Configuro VAD interno per lo stop automatico
        microphoneRecord.useVad = true;
        microphoneRecord.vadStop = true;
        microphoneRecord.vadStopTime = 2f;   // 2 s di silenzio
        microphoneRecord.dropVadPart = false; // non tagliare l’ultima parte
    }

    void ToggleRecording()
    {
        
        if (!court.PlayerCanAct)
        {
            Debug.Log("Not player's turn");
            return;
        }

        Debug.Log("Mic button pressed");
        if (!isRecording)
        {
            microphoneRecord.StartRecord();
            buttonText.text = "Stop";
            isRecording = true;
        }
        else
        {
            microphoneRecord.StopRecord();
            buttonText.text = "Record";
            isRecording = false;
        }
    }

    async void OnRecordStop(AudioChunk clip)
    {
        buttonText.text = "Record";
        isRecording = false;

        var res = await whisper.GetTextAsync(
            clip.Data, clip.Frequency, clip.Channels);

        var transcript = res?.Result ?? "";

        // Se c’è già testo, lo appendo con uno spazio 
        if (!string.IsNullOrWhiteSpace(playerInput.text))
            playerInput.text = playerInput.text.TrimEnd() + " " + transcript;
        else
            playerInput.text = transcript;

        // Riabilita e sposta il cursore in fondo
        playerInput.interactable = true;
        playerInput.caretPosition = playerInput.text.Length;
        playerInput.Select(); 
    }
}
