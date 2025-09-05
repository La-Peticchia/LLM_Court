using UnityEngine;
using UnityEngine.UI;

public class TTSMuteButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button muteButton;
    [SerializeField] private Image buttonIcon;

    [Header("Icons (Optional)")]
    [SerializeField] private Sprite muteIcon;
    [SerializeField] private Sprite unmuteIcon;

    private KokoroTTSManager ttsManager;
    private bool isMuted = false;
    private float originalVolume = 0.7f;

    private void Start()
    {
        ttsManager = FindFirstObjectByType<KokoroTTSManager>();

        if (ttsManager != null)
        {
            gameObject.SetActive(true);
            SetupButton();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void SetupButton()
    {
        if (muteButton == null)
            muteButton = GetComponent<Button>();

        muteButton.onClick.AddListener(ToggleMute);

        if (ttsManager.GetComponent<AudioSource>() != null)
        {
            originalVolume = ttsManager.GetComponent<AudioSource>().volume;
        }

        UpdateButtonVisual();
    }

    public void ToggleMute()
    {
        if (ttsManager == null) return;

        isMuted = !isMuted;

        AudioSource audioSource = ttsManager.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = ttsManager.GetComponentInChildren<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.volume = isMuted ? 0f : originalVolume;
        }

        if (isMuted)
        {
            ttsManager.StopAllSpeech();
        }

        UpdateButtonVisual();
    }

    private void UpdateButtonVisual()
    {
        if (buttonIcon != null && muteIcon != null && unmuteIcon != null)
        {
            buttonIcon.sprite = isMuted ? muteIcon : unmuteIcon;
        }

        Text buttonText = muteButton.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = isMuted ? "Mute" : "Unmute";
        }

        ColorBlock colors = muteButton.colors;
        colors.normalColor = isMuted ? Color.red : Color.white;
        muteButton.colors = colors;
    }
}