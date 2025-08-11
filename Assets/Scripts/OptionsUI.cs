using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LLMUnity;

public class OptionsUI : MonoBehaviour
{
    [Header("Audio Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_InputField masterVolumeInput;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_InputField musicVolumeInput;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_InputField sfxVolumeInput;

    [Header("LLM GPU Settings")]
    [SerializeField] private Slider gpuLayersSlider;
    [SerializeField] private TMP_InputField gpuLayersInput;
    [SerializeField] private TextMeshProUGUI gpuLayersDescription;

    [Header("Audio Test")]
    [SerializeField] private string testSFXName = "button_click";

    [Header("Buttons")]
    [SerializeField] private Button resetAllButton;

    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    public const string GPU_LAYERS_KEY = "GPULayers";

    private const float DEFAULT_MASTER_VOLUME = 80f;
    private const float DEFAULT_MUSIC_VOLUME = 70f;
    private const float DEFAULT_SFX_VOLUME = 85f;
    private const int DEFAULT_GPU_LAYERS = 15;

    private int maxGPULayers = 80;

    private void Start()
    {
        if (resetAllButton != null) resetAllButton.onClick.AddListener(ResetAllToDefault);


        SetupSliderWithInput(masterVolumeSlider, masterVolumeInput, 0, 100, OnMasterVolumeChanged);
        SetupSliderWithInput(musicVolumeSlider, musicVolumeInput, 0, 100, OnMusicVolumeChanged);
        SetupSliderWithInput(sfxVolumeSlider, sfxVolumeInput, 0, 100, OnSFXVolumeChanged);

        if (LLM.Instance != null)
            maxGPULayers = 80;

        SetupSliderWithInput(gpuLayersSlider, gpuLayersInput, 0, maxGPULayers, OnGPULayersChanged);

        LoadAllSettings();
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

    private void LoadAllSettings()
    {
        float masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_MASTER_VOLUME);
        float musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, DEFAULT_MUSIC_VOLUME);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, DEFAULT_SFX_VOLUME);
        int gpuLayers = PlayerPrefs.GetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);

        masterVolumeSlider.SetValueWithoutNotify(masterVolume);
        musicVolumeSlider.SetValueWithoutNotify(musicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);
        gpuLayersSlider.SetValueWithoutNotify(gpuLayers);

        masterVolumeInput.SetTextWithoutNotify(masterVolume.ToString("0"));
        musicVolumeInput.SetTextWithoutNotify(musicVolume.ToString("0"));
        sfxVolumeInput.SetTextWithoutNotify(sfxVolume.ToString("0"));
        gpuLayersInput.SetTextWithoutNotify(gpuLayers.ToString());

        ApplyAudioSettings(masterVolume, musicVolume, sfxVolume);
        UpdateGPUDescription(gpuLayers);
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
        masterVolumeInput.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString());
        ApplyAudioSettings(value, musicVolumeSlider.value, sfxVolumeSlider.value);
        SaveAudioSettings();
    }

    private void OnMusicVolumeChanged(float value)
    {
        musicVolumeInput.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString());
        ApplyAudioSettings(masterVolumeSlider.value, value, sfxVolumeSlider.value);
        SaveAudioSettings();
    }

    private void OnSFXVolumeChanged(float value)
    {
        sfxVolumeInput.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString());
        ApplyAudioSettings(masterVolumeSlider.value, musicVolumeSlider.value, value);
        if (!string.IsNullOrEmpty(testSFXName) && AudioManager.instance != null)
            AudioManager.instance.PlaySFXOneShot(testSFXName);
        SaveAudioSettings();
    }

    
    private void OnGPULayersChanged(float value)
    {
        int layers = Mathf.RoundToInt(value);
        gpuLayersInput.SetTextWithoutNotify(layers.ToString());
        UpdateGPUDescription(layers);
        SaveGPUSettings();
    }

    private void UpdateGPUDescription(int layers)
    {
        if (gpuLayersDescription != null)
        {
            string description = layers switch
            {
                0 => "Solo CPU - Velocità più lenta ma compatibile con tutti i sistemi",
                <= 10 => "GPU Minimale - Leggero boost di velocità, uso GPU ridotto",
                <= 30 => "GPU Bilanciato - Buon compromesso tra velocità e consumo memoria",
                <= 50 => "GPU Elevato - Velocità alta, richiede GPU potente",
                _ => "GPU Massimo - Velocità massima, richiede GPU di fascia alta"
            };
            gpuLayersDescription.text = description;
        }
    }

    
    private void SaveAudioSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, masterVolumeSlider.value);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, musicVolumeSlider.value);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolumeSlider.value);
        PlayerPrefs.Save();
    }

    private void SaveGPUSettings()
    {
        PlayerPrefs.SetInt(GPU_LAYERS_KEY, Mathf.RoundToInt(gpuLayersSlider.value));
        PlayerPrefs.Save();
    }

    
    private void ResetAllToDefault()
    {
        masterVolumeSlider.value = DEFAULT_MASTER_VOLUME;
        musicVolumeSlider.value = DEFAULT_MUSIC_VOLUME;
        sfxVolumeSlider.value = DEFAULT_SFX_VOLUME;
        gpuLayersSlider.value = DEFAULT_GPU_LAYERS;
    }

    
    public static int GetSavedGPULayers()
    {
        return PlayerPrefs.GetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);
    }

    
    public static void ApplyGPUSettingsToLLM()
    {
        int savedGPULayers = GetSavedGPULayers();
        if (LLM.Instance != null && !LLM.Instance.started)
        {
            LLM.Instance.numGPULayers = savedGPULayers;
            Debug.Log($"GPU Layers impostati a: {savedGPULayers}");
        }
    }
}
