using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Whisper;
using Whisper.Utils;

public class MicrophoneInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Dependencies")]
    public WhisperManager whisper;
    public MicrophoneRecord microphoneRecord;

    [Header("UI")]
    public Button micButton;
    public Image micIcon;
    public Sprite micIdleSprite;         // microfono rosso
    public Sprite micRecSprite;          // cerchietto rosso
    public Sprite micDisabledSprite;     // microfono grigio
    public InputField playerInput;
    public Court court;

    private bool isRecording = false;
    private Vector3 defaultScale;

    void Awake()
    {
        defaultScale = micButton.transform.localScale;
        micButton.onClick.AddListener(ToggleRecording);
        microphoneRecord.OnRecordStop += OnRecordStop;

        whisper.language = "";
        microphoneRecord.useVad = true;
        microphoneRecord.vadStop = true;
        microphoneRecord.vadStopTime = 2f;
        microphoneRecord.dropVadPart = false;
    }

    void Update()
    {
        if (!court.PlayerCanAct)
        {
            micButton.interactable = false;
            micIcon.sprite = micDisabledSprite;
        }
        else if (!isRecording)
        {
            micButton.interactable = true;
            micIcon.sprite = micIdleSprite;
        }
    }

    void ToggleRecording()
    {
        if (!court.PlayerCanAct)
        {
            Debug.Log("Not player's turn");
            return;
        }

        if (!isRecording)
        {
            microphoneRecord.StartRecord();
            isRecording = true;
            micIcon.sprite = micRecSprite;
        }
        else
        {
            microphoneRecord.StopRecord();
            isRecording = false;
            micIcon.sprite = micIdleSprite;
        }
    }

    async void OnRecordStop(AudioChunk clip)
    {
        isRecording = false;
        micIcon.sprite = micIdleSprite;

        var res = await whisper.GetTextAsync(clip.Data, clip.Frequency, clip.Channels);
        var transcript = res?.Result ?? "";

        if (!string.IsNullOrWhiteSpace(playerInput.text))
            playerInput.text = playerInput.text.TrimEnd() + " " + transcript;
        else
            playerInput.text = transcript;

        playerInput.interactable = true;
        playerInput.caretPosition = playerInput.text.Length;
        playerInput.Select();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        micButton.transform.localScale = defaultScale * 1.2f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        micButton.transform.localScale = defaultScale;
    }

    void OnDestroy()
    {
        microphoneRecord.OnRecordStop -= OnRecordStop;
    }

}
