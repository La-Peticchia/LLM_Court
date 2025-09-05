using LLMUnity;
using System.Collections;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class LLMSettingsInitializer : MonoBehaviour
{
    private const string GPU_LAYERS_KEY = "GPULayers";
    private const string SELECTED_MODEL_KEY = "SelectedModel";
    private const int DEFAULT_GPU_LAYERS = 15;

    private void Awake()
    {
        if (!ValidateSettings())
        {
            Debug.LogWarning("Impostazioni LLM non valide, resettate a default.");
        }
        ApplyLLMSettings();
    }

    private void ApplyLLMSettings()
    {
        LLM llmInstance = FindFirstObjectByType<LLM>();
        if (llmInstance != null)
        {
            if (!llmInstance.started)
            {
                ApplyGPUSettings(llmInstance);
                ApplyModelSelection(llmInstance);
            }
            else
            {
                Debug.LogWarning("[LLMSettingsInitializer] LLM già avviato, impossibile modificare le impostazioni");
            }
        }
        else
        {
            Debug.Log("[LLMSettingsInitializer] LLM non ancora presente, ritento dopo un attimo");
            StartCoroutine(RetryApplyAfterDelay(0.5f));
        }
    }

    private void ApplyGPUSettings(LLM llmInstance)
    {
        int savedGPULayers = GetSavedGPULayers();
        llmInstance.numGPULayers = savedGPULayers;
        Debug.Log($"[LLMSettingsInitializer] GPU Layers applicati: {savedGPULayers}");
    }

    private void ApplyModelSelection(LLM llmInstance)
    {
        string selectedModel = GetSelectedModel();

        if (!string.IsNullOrEmpty(selectedModel))
        {
            llmInstance.SetModel(selectedModel);
            Debug.Log($"[LLMSettingsInitializer] Modello applicato: {selectedModel}");
        }
    }



    /* private string FindModelPath(string modelName)
     {
         // Cerca nei percorsi standard dove possono essere memorizzati i modelli
         string[] searchPaths = {
             Application.streamingAssetsPath,
             Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "LLMUnity"),
             Path.Combine(Application.persistentDataPath, "models"),
             Path.Combine(Application.persistentDataPath, "LLMUnity")
         };

         foreach (string searchPath in searchPaths)
         {
             if (Directory.Exists(searchPath))
             {
                 // Cerca ricorsivamente
                 string[] files = Directory.GetFiles(searchPath, modelName, SearchOption.AllDirectories);
                 if (files.Length > 0)
                 {
                     return files[0];
                 }

                 // Cerca anche file .gguf che corrispondono al nome
                 string[] ggufFiles = Directory.GetFiles(searchPath, "*.gguf", SearchOption.AllDirectories);
                 foreach (string file in ggufFiles)
                 {
                     if (Path.GetFileName(file).Equals(modelName, System.StringComparison.OrdinalIgnoreCase))
                     {
                         return file;
                     }
                 }
             }
         }

         // Prova anche a utilizzare il sistema di gestione modelli di LLMUnity
         try
         {
             string managerPath = LLM.GetLLMManagerAssetRuntime(modelName);
             if (!string.IsNullOrEmpty(managerPath) && File.Exists(managerPath))
             {
                 return managerPath;
             }
         }
         catch (System.Exception e)
         {
             Debug.LogWarning($"[LLMSettingsInitializer] Errore nel cercare il modello tramite LLMManager: {e.Message}");
         }

         return null;
     }*/

    private IEnumerator RetryApplyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyLLMSettings();
    }

    public static int GetSavedGPULayers()
    {
        return PlayerPrefs.GetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);
    }

    public static string GetSelectedModel()
    {
        return PlayerPrefs.GetString(SELECTED_MODEL_KEY, "");
    }

    public bool ValidateSettings()
    {
        bool isValid = true;

        // Valida GPU Layers
        int gpuLayers = GetSavedGPULayers();
        if (gpuLayers < 0 || gpuLayers > 80)
        {
            Debug.LogWarning($"[LLMSettingsInitializer] GPU Layers fuori range: {gpuLayers}. Resetting to {DEFAULT_GPU_LAYERS}.");
            PlayerPrefs.SetInt(GPU_LAYERS_KEY, DEFAULT_GPU_LAYERS);
            isValid = false;
        }

        // Valida selezione modello (solo che non sia stringa vuota)
        string selectedModel = GetSelectedModel();
        if (!string.IsNullOrEmpty(selectedModel))
        {
            Debug.Log($"[LLMSettingsInitializer] Modello selezionato: {selectedModel}");
        }

        if (!isValid)
        {
            PlayerPrefs.Save();
        }

        return isValid;
    }

}