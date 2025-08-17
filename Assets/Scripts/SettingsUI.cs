using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine.EventSystems;
using LLMUnity;

public class SettingsUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject settingsUI;
    [SerializeField] private GameObject mainSettingsPanel;
    [SerializeField] private GameObject audioSettingsPanel;
    [SerializeField] private Court court;
    [SerializeField] private SavePopupUI savePopupUI;

    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Button settingsButton;        
    [SerializeField] private Button audioSettingsButton;   
    [SerializeField] private Button resetVolumeButton;
    [SerializeField] private Button retryButton;

    [Header("Audio Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_InputField masterVolumeInputField;
    [SerializeField] private Slider BGVolumeSlider;
    [SerializeField] private TMP_InputField BGVolumeInputField;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_InputField sfxVolumeInputField;

    [Header("Audio Test")]
    [SerializeField] private string testSFXName = "button_click";

    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string BG_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    private const float DEFAULT_MASTER_VOLUME = 80f;
    private const float DEFAULT_MUSIC_VOLUME = 70f;
    private const float DEFAULT_SFX_VOLUME = 85f;

    public bool IsOpen => settingsUI != null && settingsUI.activeInHierarchy;

    private void Start()
    {
        settingsUI.SetActive(false);
        mainSettingsPanel.SetActive(true);
        audioSettingsPanel.SetActive(false);

        settingsButton?.onClick.AddListener(OpenSettings);
        returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
        audioSettingsButton.onClick.AddListener(OpenAudioSettings);
        resetVolumeButton.onClick.AddListener(ResetVolumesToDefault);
        retryButton.onClick.AddListener(RetrySameCase);

        SetupSliderWithInput(masterVolumeSlider, masterVolumeInputField, 0, 100, OnMasterVolumeChanged);
        SetupSliderWithInput(BGVolumeSlider, BGVolumeInputField, 0, 100, OnBGVolumeChanged);
        SetupSliderWithInput(sfxVolumeSlider, sfxVolumeInputField, 0, 100, OnSFXVolumeChanged);

        LoadVolumeSettings();
    }

    private void RetrySameCase()
    {
        SaveVolumeSettings();

        if (court != null)
        {
            CaseMemory.SavedCase = court.GetCaseDescription();
            CaseMemory.SavedTranslatedCase = court.GetTranslatedDescription();
            CaseMemory.RestartingSameCase = true;

            // Salva il seed corrente e genera uno nuovo
            LLMCharacter llmCharacter = FindFirstObjectByType<LLMCharacter>();
            if (llmCharacter != null)
            {
                CaseMemory.OriginalSeed = llmCharacter.seed; // Salva il seed attuale
                llmCharacter.ClearSavedHistory(); // Cancella cronologia
            }

            int newAISeed = Random.Range(0, int.MaxValue);
            CaseMemory.NewAISeed = newAISeed;

            Debug.Log($"[RETRY] Seed originale: {CaseMemory.OriginalSeed}, Nuovo seed: {newAISeed}");
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void SetupSliderWithInput(Slider slider, TMP_InputField input, int min, int max, UnityEngine.Events.UnityAction<float> onSliderChange)
    {
        if (slider != null)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(onSliderChange);
        }

        if (input != null)
        {
            input.onEndEdit.AddListener((string val) =>
            {
                if (int.TryParse(val, out int num))
                {
                    num = Mathf.Clamp(num, min, max);
                    if (slider != null)
                    {
                        slider.SetValueWithoutNotify(num);
                        onSliderChange(num);
                    }
                }
                else
                {
                    if (slider != null)
                        input.SetTextWithoutNotify(Mathf.RoundToInt(slider.value).ToString());
                }
            });
        }
    }

    private void LoadVolumeSettings()
    {
        float masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_MASTER_VOLUME);
        float musicVolume = PlayerPrefs.GetFloat(BG_VOLUME_KEY, DEFAULT_MUSIC_VOLUME);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);

        masterVolumeSlider.SetValueWithoutNotify(masterVolume);
        BGVolumeSlider.SetValueWithoutNotify(musicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);

        masterVolumeInputField.SetTextWithoutNotify(Mathf.RoundToInt(masterVolume).ToString());
        BGVolumeInputField.SetTextWithoutNotify(Mathf.RoundToInt(musicVolume).ToString());
        sfxVolumeInputField.SetTextWithoutNotify(Mathf.RoundToInt(sfxVolume).ToString());

        ApplyAudioSettings(masterVolume, musicVolume, sfxVolume);
    }

    private void ApplyAudioSettings(float master, float music, float sfx)
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.SetMasterVolume(master / 100f);
            AudioManager.instance.SetMusicVolume(music / 100f);
            AudioManager.instance.SetSFXVolume(sfx / 100f);
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        masterVolumeInputField.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString());

        ApplyAudioSettings(value, BGVolumeSlider.value, sfxVolumeSlider.value);
        SaveVolumeSettings();
    }


    private void OnBGVolumeChanged(float value)
    {
        BGVolumeInputField.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString());

        ApplyAudioSettings(masterVolumeSlider.value, value, sfxVolumeSlider.value);
        SaveVolumeSettings();
    }

    private void OnSFXVolumeChanged(float value)
    {
        sfxVolumeInputField.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString());

        ApplyAudioSettings(masterVolumeSlider.value, BGVolumeSlider.value, value);

        if (!string.IsNullOrEmpty(testSFXName) && AudioManager.instance != null)
            AudioManager.instance.PlaySFXOneShot(testSFXName);

        SaveVolumeSettings();
    }

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, masterVolumeSlider.value);
        PlayerPrefs.SetFloat(BG_VOLUME_KEY, BGVolumeSlider.value);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolumeSlider.value);
        PlayerPrefs.Save();
    }

    private void ResetVolumesToDefault()
    {
        masterVolumeSlider.value = DEFAULT_MASTER_VOLUME;
        BGVolumeSlider.value = DEFAULT_MUSIC_VOLUME;
        sfxVolumeSlider.value = DEFAULT_SFX_VOLUME;
    }

    private void Update()
    {
        if (settingsUI.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
        {
            if (audioSettingsPanel.activeInHierarchy)
            {
                CloseAudioSettings();
            }
            else
            {
                CloseSettings();
            }
        }
    }

    public void OpenSettings()
    {
        settingsUI.SetActive(true);
        mainSettingsPanel.SetActive(true);
        audioSettingsPanel.SetActive(false);

        LoadVolumeSettings();
    }

    public void CloseSettings()
    {
        settingsUI.SetActive(false);
 
        SaveVolumeSettings();
    }

    public void OpenAudioSettings()
    {
        mainSettingsPanel.SetActive(false);
        audioSettingsPanel.SetActive(true);

        LoadVolumeSettings();
    }

    public void CloseAudioSettings()
    {
        audioSettingsPanel.SetActive(false);
        mainSettingsPanel.SetActive(true);

        SaveVolumeSettings();
    }

    private void ReturnToMainMenu()
    {
        SaveVolumeSettings();

        if (savePopupUI != null)
        {
            settingsUI.SetActive(false);
            savePopupUI.ShowSavePopup(SavePopupUI.ReturnDestination.MainMenu);
        }
        else
        {
            SceneManager.LoadScene("Menu");
        }
    }
}
