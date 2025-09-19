using UnityEngine;
using UnityEngine.UI;

public class TTSMuteButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button muteButton;
    [SerializeField] private Image buttonIcon;

    [Header("Icons")]
    [SerializeField] private Sprite muteIcon;
    [SerializeField] private Sprite unmuteIcon;

    [Header("Resume Behavior")]
    [SerializeField] private float catchUpThreshold = 3f;
    [SerializeField] private bool showMuteIndicator = true;

    private KokoroTTSManager ttsManager;
    private Court court;
    private bool isMuted = false;
    private float muteStartTime;
    private float originalVolume = 0.7f;
    private bool lastTTSState = false;

    private void Start()
    {
        ttsManager = FindFirstObjectByType<KokoroTTSManager>();
        court = FindFirstObjectByType<Court>();

        if (ttsManager != null && court != null)
        {
            SetupButton();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (court != null)
        {
            bool currentTTSState = court.enableTTS;
            if (currentTTSState != lastTTSState)
            {
                OnTTSStateChanged(currentTTSState);
                lastTTSState = currentTTSState;
            }
        }
    }

    private void OnTTSStateChanged(bool ttsEnabled)
    {
        gameObject.SetActive(ttsEnabled);

        if (showMuteIndicator)
        {
            Debug.Log($"[TTS Button] TTS enabled: {ttsEnabled}, Button visible: {ttsEnabled}");
        }

        if (!ttsEnabled && isMuted)
        {
            ForceUnmute();
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

        if (isMuted)
        {
            // QUANDO MUTO: Pausa il personaggio corrente + imposta volume a 0
            muteStartTime = Time.time;

            AudioSource audioSource = GetAudioSource();
            if (audioSource != null)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Pause();
                }

                audioSource.volume = 0f;
            }

            if (showMuteIndicator)
            {
                Debug.Log("[TTS] Muted - Current paused + Volume set to 0");
            }
        }
        else
        {
            // QUANDO SMUTO: Resume intelligente + ripristina volume
            float muteDuration = Time.time - muteStartTime;

            AudioSource audioSource = GetAudioSource();
            if (audioSource != null)
            {
                audioSource.volume = originalVolume;

                if (muteDuration <= catchUpThreshold && audioSource.clip != null)
                {
                    // Resume normale - riprendi dal punto esatto
                    audioSource.UnPause();
                    if (showMuteIndicator)
                    {
                        Debug.Log($"[TTS] Short mute ({muteDuration:F1}s) - Resuming from exact position");
                    }
                }
                else
                {
                    // Mute lungo - skippa la clip corrente, ma il volume è ripristinato per i prossimi
                    if (showMuteIndicator)
                    {
                        Debug.Log($"[TTS] Long mute ({muteDuration:F1}s) - Volume restored for next speech");
                    }
                }
            }
        }

        UpdateButtonVisual();
    }

    private AudioSource GetAudioSource()
    {
        AudioSource audioSource = ttsManager.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = ttsManager.GetComponentInChildren<AudioSource>();
        }
        return audioSource;
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
            buttonText.text = isMuted ? "Unmute" : "Mute";
        }

    }

    //public bool IsMuted() => isMuted;

    public void ForceUnmute()
    {
        if (isMuted) ToggleMute();
    }
}
