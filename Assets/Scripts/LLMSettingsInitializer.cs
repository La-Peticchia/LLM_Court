using LLMUnity;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class LLMSettingsInitializer : MonoBehaviour
{
    private const string GPU_LAYERS_KEY = "GPULayers";
    private const int DEFAULT_GPU_LAYERS = 15;

    private void Awake()
    {
        if (!ValidateSettings())
        {
            Debug.LogWarning("Impostazioni GPU non valide, resettate a default.");
        }

            ApplyGPUSettings();
    }

    private void ApplyGPUSettings()
    {
        int savedGPULayers = PlayerPrefs.GetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);

        LLM llmInstance = FindFirstObjectByType<LLM>();

        if (llmInstance != null)
        {
            if (!llmInstance.started)
            {
                llmInstance.numGPULayers = savedGPULayers;
                Debug.Log($"[LLMSettingsInitializer] GPU Layers applicati: {savedGPULayers}");
            }
            else
            {
                Debug.LogWarning("[LLMSettingsInitializer] LLM già avviato, impossibile modificare GPU layers");
            }
        }
        else
        {
            Debug.Log("[LLMSettingsInitializer] LLM non ancora presente, ritento dopo un attimo");

            StartCoroutine(RetryApplyAfterDelay(0.5f));
        }
    }

    private IEnumerator RetryApplyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyGPUSettings();
    }

    public static int GetSavedGPULayers()
    {
        return PlayerPrefs.GetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);
    }

 
    public bool ValidateSettings()
    {
        int gpuLayers = GetSavedGPULayers();

        if (gpuLayers < 0 || gpuLayers > 80)
        {
            Debug.LogWarning($"[LLMSettingsInitializer] GPU Layers fuori range: {gpuLayers}. Resetting to 15.");
            PlayerPrefs.SetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);
            PlayerPrefs.Save();
            return false;
        }

        return true;
    }
}