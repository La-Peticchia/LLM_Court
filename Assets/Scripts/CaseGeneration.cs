using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
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
    [SerializeField] private Button returnToMenuButton;

    private LinkedList<CaseDescription> _translatedDescriptions;
    
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


        _translatedDescriptions = new LinkedList<CaseDescription>();

        if (seed == 0)
            seed = Random.Range(0, int.MaxValue);
        
        _courtRecordUI.isGameplay = false;


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
        
        CaseDescription transCaseDescription = _translatedDescriptions.First.Value;
        CaseDescription tmpCaseDescription = null;
        
        //TODO implement a new class that manages the save files of cases; implement both saving and loading procedure with some basic UI that shows all the loaded files
        
        if (_saveManager.CheckForTranslation(transCaseDescription.GetID()))
        {
            
            Debug.Log("Case description ID: " + transCaseDescription.GetID());
            CaseDescription[] tmpDescriptions = _saveManager.GetSavedDescriptionsByID(transCaseDescription.GetID());
            tmpCaseDescription = tmpDescriptions[0];
        }
        else
        {
            try
            {
                string response = await _apiManager.RequestTranslation(transCaseDescription.GetJsonDescription(), seed: seed);
                tmpCaseDescription = JsonConvert.DeserializeObject<CaseDescription>(response);
                
                if(transCaseDescription.IsSaved())
                    _saveManager.SaveCaseDescription(new []{tmpCaseDescription, transCaseDescription}, transCaseDescription.GetID());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Translation failed:" + e.Message);
            }
        
        }

        _court.InitializeCourt(tmpCaseDescription ?? JsonConvert.DeserializeObject<CaseDescription>(JsonConvert.SerializeObject(transCaseDescription)), transCaseDescription);
        _saveManager.SaveAsLastCase(tmpCaseDescription != null ? new[]{tmpCaseDescription, transCaseDescription} : new []{transCaseDescription});
        
        
        courtPreviewCanvas.SetActive(false);
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
                value = _translatedDescriptions.Last.Value.GetBriefDescription(true);
                _translatedDescriptions.AddFirst(_translatedDescriptions.Last.Value);
                _translatedDescriptions.RemoveLast();
                break;
            case 1:
                _translatedDescriptions.AddLast(_translatedDescriptions.First.Value);
                _translatedDescriptions.RemoveFirst();
                value = _translatedDescriptions.First.Value.GetBriefDescription(true);
                break;
            default:
                value = "ERROR - Wrong button ID";
                break;
        }
        
        await _courtPreviewAnimation.PlaySwitchAnimation(buttonID,value);
        ToggleSaveButton();
        ToggleButtons(true);
        
    }

    void ToggleSaveButton()
    {
        if (_translatedDescriptions.First.Value.IsSaved())
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

    void OnSaveButtonClicked()
    {
        var firstDes = _translatedDescriptions.First.Value;
        if (firstDes.IsSaved())
        {
            _saveManager.DeleteDescription(firstDes.GetID());
            firstDes.SetID(-1);
        }
        else
            firstDes.SetID(_saveManager.SaveCaseDescription(new []{firstDes}));
        
        ToggleSaveButton();
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
        CaseDescription tmpDescription = await _apiManager.RequestCaseDescription(prefInputField.text, true, seed);
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
            
            _translatedDescriptions.AddFirst(description);
            await _courtPreviewAnimation.PlayAnimation(description.GetBriefDescription(true));
            ToggleSaveButton();
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
