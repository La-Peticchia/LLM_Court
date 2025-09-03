using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using LLMUnity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Windows;
using File = System.IO.File;
using Random = UnityEngine.Random;

public class CaseGeneration : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button newCaseButton;
    [SerializeField] private Button[] arrowButtons;
    [SerializeField] private TextMeshProUGUI prefInputField;
    [SerializeField] private TextMeshProUGUI errorTextbox;
    [SerializeField] private GameObject courtPreviewCanvas;
    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] public Button returnToMenuButton;

    private LinkedList<CaseDescription> _descriptionList;
    private string _language;
    
    //References
    private APIInterface _apiManager;
    private CourtPreviewAnimation _courtPreviewAnimation;
    private Court _court;
    private SaveSystem _saveManager;
    
    [FormerlySerializedAs("_seed")] [SerializeField]
    private int seed;
    
    private CourtRecordUI _courtRecordUI;

    private void Awake()
    {
        _apiManager = FindFirstObjectByType<APIInterface>();
        _saveManager = FindFirstObjectByType<SaveSystem>();
        _court = FindFirstObjectByType<Court>();
        _courtPreviewAnimation = FindFirstObjectByType<CourtPreviewAnimation>();
        playButton.onClick.AddListener(OnPlayButtonClicked);
        _courtRecordUI = FindFirstObjectByType<CourtRecordUI>();

        newCaseButton.onClick.AddListener(OnNewCaseButtonClicked);
        saveButton.onClick.AddListener(OnSaveButtonClicked);
        
        
        returnToMenuButton.onClick.AddListener(ReturnToMainMenu);


        _descriptionList = new LinkedList<CaseDescription>();

        if (seed == 0)
            seed = Random.Range(0, int.MaxValue);
        
        _courtRecordUI.isGameplay = false;

        //Debug
        //_language = "french";
        //_language = PlayerPrefs.GetString("language");
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // se l'utente clicca contine ed ha un salvataggio parte direttamente il gameplay senza passare per court preview canvas
        if (PlayerPrefs.GetInt("UseLastSavedCase", 0) == 1)
        {
            PlayerPrefs.SetInt("UseLastSavedCase", 0);
            PlayerPrefs.Save();

            SaveSystem saveSystem = FindFirstObjectByType<SaveSystem>();
            var lastCase = saveSystem?.GetLastSavedCase();

            if (lastCase != null && lastCase.Length > 0)
            {
                courtPreviewCanvas.SetActive(false);
                loadingCanvas.SetActive(true);

                // Passa la traduzione se esiste, altrimenti usa la descrizione originale come fallback
                if (lastCase.Length > 1)
                    _court.InitializeCourt(lastCase[0], lastCase[1]);
                else
                    _court.InitializeCourt(JsonConvert.DeserializeObject<CaseDescription>(JsonConvert.SerializeObject(lastCase[0])), lastCase[0]);

                Destroy(gameObject);
                return;
            }
        }

        if (CaseMemory.NewAISeed.HasValue)
        {
            // Trova l'LLMCharacter e aggiorna il suo seed
            LLMCharacter llmCharacter = FindFirstObjectByType<LLMCharacter>();
            if (llmCharacter != null)
            {
                
                int oldSeed = llmCharacter.seed;

                int newSeed = CaseMemory.NewAISeed.Value;

                Debug.Log($"[CASE_GEN] Vecchio seed: {oldSeed}, Nuovo seed: {newSeed}");

                llmCharacter.UpdateSeed(newSeed);
            }
            else
            {
                Debug.LogWarning("[CASE_GEN] LLMCharacter non trovato per aggiornare il seed");
            }
        }


        if (CaseMemory.HasValidSavedCase)
        {
            courtPreviewCanvas.SetActive(false);
            _court.InitializeCourt(CaseMemory.SavedCase, CaseMemory.SavedTranslatedCase);
            CaseMemory.Clear();
            Destroy(gameObject);
            return;
        }

        //if (GameSaveSystem.IsContinue && GameSaveSystem.HasSavedGame())
        //{
        //    var data = GameSaveSystem.LoadGame();
        //    courtPreviewCanvas.SetActive(false);
        //    // Ricostruisce il CaseDescription da JSON salvato
        //    CaseDescription savedCase = new CaseDescription(
        //        data.caseDescription, sectionSplitCharacters: "\n", subsectionSplitCharacters: "\n"
        //    );
        //    CaseDescription savedTranslatedCase = new CaseDescription(
        //        data.translatedDescription, sectionSplitCharacters: "\n", subsectionSplitCharacters: "\n"
        //    );
        //    // Inizializza court con i dati salvati e mi assicuro che riprenda dal round corretto
        //    _court.InitializeCourt(savedCase, savedTranslatedCase);
        //    _court.SetCurrentRound(data.round);
        //    GameSaveSystem.IsContinue = false;
        //    Destroy(gameObject);
        //    return;
        //}

    }
    
    CaseDescription _currentCaseDescription;
    CaseDescription _currentTranslatedDescription;
    //private Witness[] _currentWitnesses;
    private int _currentSeed;


    private void ReturnToMainMenu()
    {
        SceneManager.LoadScene("Menu");
    }

    async void OnPlayButtonClicked()
    {
        ToggleButtons(false);

        CaseDescription firstCaseDescription = _descriptionList.First.Value;
        CaseDescription tmpCaseDescription = null;

        if(!firstCaseDescription.language.ToLower().StartsWith("en"))
            if (_saveManager.CheckForEnglish(firstCaseDescription.GetID()))
            {
                Debug.Log("Case description ID: " + firstCaseDescription.GetID());
                CaseDescription[] tmpDescriptions = _saveManager.GetSavedDescriptionsByID(firstCaseDescription.GetID());
                tmpCaseDescription = tmpDescriptions[0];
            }
            else
            {
                try
                {
                    string response = await _apiManager.RequestTranslation(firstCaseDescription.GetJsonDescription(), "english", seed);
                    tmpCaseDescription = JsonConvert.DeserializeObject<CaseDescription>(response);

                    if (firstCaseDescription.IsSaved())
                        await _saveManager.SaveCaseDescription(new[] { tmpCaseDescription, firstCaseDescription }, firstCaseDescription.GetID());
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Translation failed:" + e.Message);
                }
            }

        await _saveManager.SaveAsLastCase(tmpCaseDescription != null ? new[] { tmpCaseDescription, firstCaseDescription } : new[] { firstCaseDescription });

        courtPreviewCanvas.SetActive(false);

        if (loadingCanvas != null)
            loadingCanvas.SetActive(true);

        KokoroTTSManager ttsManager = FindFirstObjectByType<KokoroTTSManager>();
        if (ttsManager != null)
        {
            int timeout = 0;
            while (!ttsManager.isInitialized && timeout < 50) // Max 5 secondi
            {
                await System.Threading.Tasks.Task.Delay(100);
                timeout++;
            }

            if (!ttsManager.isInitialized)
                Debug.LogWarning("TTS not ready, continuing without it");
        }

        _court.InitializeCourt(
            tmpCaseDescription ?? JsonConvert.DeserializeObject<CaseDescription>(JsonConvert.SerializeObject(firstCaseDescription)),
            firstCaseDescription
        );

        if (loadingCanvas != null)
            loadingCanvas.SetActive(false);

        Destroy(gameObject);
        AudioManager.instance.PlayMusicForScene("Gameplay");
    }

    private void OnNewCaseButtonClicked()
    {
        StoreDescriptions();
    }

    public async void OnArrowClicked(int buttonID)
    {
        ToggleButtons(false);
        string value;

        switch (buttonID)
        {
            case 0 :
                value = _descriptionList.Last.Value.GetBriefDescription(true);
                _descriptionList.AddFirst(_descriptionList.Last.Value);
                _descriptionList.RemoveLast();
                break;
            case 1:
                _descriptionList.AddLast(_descriptionList.First.Value);
                _descriptionList.RemoveFirst();
                value = _descriptionList.First.Value.GetBriefDescription(true);
                break;
            default:
                value = "ERROR - Wrong button ID";
                break;
        }
        
        await _courtPreviewAnimation.PlaySwitchAnimation(buttonID,value);
        ToggleSaveButton();
        ToggleButtons(true);
        
        Debug.Log("ID: " + _descriptionList.First.Value.GetID());
    }

    void ToggleSaveButton()
    {
        if (_descriptionList.First.Value.IsSaved())
        {
            saveButton.GetComponentInChildren<TextMeshProUGUI>().text = "delete";
            saveButton.GetComponent<Image>().color = new Color(255, 179, 179);
        }
        else
        {
            saveButton.GetComponentInChildren<TextMeshProUGUI>().text = "save";
            saveButton.GetComponent<Image>().color = new Color(207, 255, 179);
        }
        
    }

    async void OnSaveButtonClicked()
    {
        ToggleButtons(false);
        var firstDes = _descriptionList.First.Value;
        if (firstDes.IsSaved())
        {
            _saveManager.DeleteDescription(firstDes.GetID());
            firstDes.SetID(-1);
        }
        else
            firstDes.SetID(await _saveManager.SaveCaseDescription(new []{firstDes}));
        
        ToggleSaveButton();
        ToggleButtons(true);
    }

    private async Task OnError(string error)
    {
        ToggleButtons(true);
        errorTextbox.text = error;
        errorTextbox.enabled = true;
        await Task.Delay(5000);
        errorTextbox.enabled = false;
    }

    private async void StoreDescriptions()
    {
        ToggleButtons(false);
        
        
        //Get translated case description
        CaseDescription tmpDescription = await _apiManager.RequestCaseDescription(prefInputField.text, _language, seed);
        if (tmpDescription != null)
            AddDescriptionToList(tmpDescription);
        else
        {
            _ = OnError("Case generation failed, retry");
            seed = Random.Range(0, int.MaxValue);
        }


        ToggleButtons(true);
    }

    public async void AddDescriptionToList(CaseDescription description)
    {
            
            _descriptionList.AddFirst(description);
            await _courtPreviewAnimation.PlayAnimation(description.GetBriefDescription(true));
            ToggleSaveButton();
            
        Debug.Log("ID: " + description.GetID());
    }

    private void ToggleButtons(bool enable)
    {
        saveButton.interactable = enable;
        playButton.interactable = enable;
        newCaseButton.interactable = enable;
        foreach (var item in arrowButtons)
            item.enabled = enable;
    }
    
}
