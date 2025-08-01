using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Windows;
using File = System.IO.File;
using Random = UnityEngine.Random;

public class CaseGeneration : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button newCaseButton;
    [SerializeField] private Button[] arrowButtons;
    [SerializeField] private TextMeshProUGUI prefInputField;
    [SerializeField] private TextMeshProUGUI errorTextbox;
    [SerializeField] private GameObject courtPreviewCanvas;
    
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
        
        
        _translatedDescriptions = new LinkedList<CaseDescription>();

        if (seed == 0)
            seed = Random.Range(0, int.MaxValue);
        
        _courtRecordUI.isGameplay = false;

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (CaseMemory.HasValidSavedCase)
        {
            courtPreviewCanvas.SetActive(false);
            _court.InitializeCourt(CaseMemory.SavedCase.Value, CaseMemory.SavedTranslatedCase.Value);
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

        StoreDescriptions();
    }
    
    CaseDescription _currentCaseDescription;
    CaseDescription _currentTranslatedDescription;
    //private Witness[] _currentWitnesses;
    private int _currentSeed;
    
//    
//    public async void OnPlayButtonClicked()
//    {
//        ToggleButtons(false);
//
//        //TODO create another method that outputs a rich text of the case in the new JSONCaseDescription class. In addition, in this method you should call other methods in APIInterface to create the english version of the description and the witnesses
//        //TODO Add temporary variables so that you can retry without doing all the requests again   
//
//        if (Equals(_currentTranslatedDescription, _translatedDescriptions.First.Value))
//        {
//            _currentSeed = Random.Range(0, int.MaxValue);
//            if (!string.IsNullOrWhiteSpace(_currentCaseDescription.summary))
//            {
//                if (_currentWitnesses is { Length: > 0 })
//                    goto Witness1;
//                else
//                    goto Witness;
//            }
//            else
//                goto Description;
//        }
//        
//        _currentCaseDescription = new JSONCaseDescription();
//        _currentTranslatedDescription = new JSONCaseDescription();
//        _currentWitnesses = new Witness[]{};
//        _currentTranslatedDescription = _translatedDescriptions.First.Value;
//        _currentSeed = _seed;
//        
//        Description:
//        
//        Debug.Log(_currentTranslatedDescription.title);
//        try
//        {
//            JSONCaseDescription translatedDescription1 = _currentTranslatedDescription;
//            translatedDescription1.sectionNames = new List<string>();
//
//            string response =
//                await _apiManager.RequestTranslation(translatedDescription1.GetJsonDescription(), seed: _currentSeed);
//            var root = JObject.Parse(response);
//
//            if (root.ContainsKey("translation"))
//                _currentCaseDescription = root["translation"]!.ToObject<JSONCaseDescription>();
//            else if (root.ContainsKey("translations"))
//                _currentCaseDescription = root["translations"]!.ToObject<JSONCaseDescription>();
//            else
//                _currentCaseDescription = JsonConvert.DeserializeObject<JSONCaseDescription>(response);
//            
//        }
//        catch (Exception e)
//        {
//            Debug.LogWarning("JSON object creation failed:" + e.Message);
//            _ = OnError("Translation error, retry");
//            return;
//        }
//        
//        
//        Witness:
//        
//        _currentWitnesses = await _apiManager.RequestWitnesses(_currentCaseDescription.GetTotalDescription(new int[]{0,2,3}), _currentSeed);
//        if (_currentWitnesses == null || _currentWitnesses.Length == 0)
//        {
//            _ = OnError("Witness Generation error, retry");
//            return;
//        }
//        
//        Witness1:
//        
//        //TODO you must edit the API Request methods signature to accept a seed so that every step in this method can be retried with a different seed and possibly succeed
//        
//        Witness[] transWitnesses;
//        try
//        {
//
//            string response = await _apiManager.RequestTranslation(JsonConvert.SerializeObject(_currentWitnesses, Formatting.None), "italian", _currentSeed);
//
//            var root = JObject.Parse(response);
//            transWitnesses = root.ContainsKey("witnesses") ? root["translations"]?.ToObject<Witness[]>() : JsonConvert.DeserializeObject<Witness[]>(response);
//            
//        }
//        catch (Exception e)
//        {
//            Debug.LogWarning("JSON object creation failed:" + e.Message);
//            _ = OnError("Witness Translation error, retry");
//            return;
//        }
//
//        _currentCaseDescription.SetWitnesses(_currentWitnesses);
//        _currentTranslatedDescription.SetWitnesses(transWitnesses);
//        
//        SaveCaseDescription(new []{_currentCaseDescription, _currentTranslatedDescription});
//        
//        _court.InitializeCourt(_currentCaseDescription, _currentTranslatedDescription);
//        courtPreviewCanvas.SetActive(false);
//        Destroy(gameObject);
//    }
//    

    async void OnPlayButtonClicked()
    {
        ToggleButtons(false);
        
        CaseDescription transCaseDescription = _translatedDescriptions.First.Value;
        
        
        //TODO implement a new class that manages the save files of cases; implement both saving and loading procedure with some basic UI that shows all the loaded files

        if (transCaseDescription.IsSaved())
        {
            if (_saveManager.CheckForTranslation(transCaseDescription.GetID()))
            {
                CaseDescription[] tmpDescriptions = _saveManager.GetSavedDescriptionsByID(transCaseDescription.GetID());
                _court.InitializeCourt(tmpDescriptions[0], tmpDescriptions[1]);
            }
            else
            {
                try
                {
                    string response = await _apiManager.RequestTranslation(transCaseDescription.GetJsonDescription(), seed: seed);
                    CaseDescription tmpCaseDescription = JsonConvert.DeserializeObject<CaseDescription>(response);
                    _saveManager.SaveCaseDescription(new []{tmpCaseDescription, transCaseDescription}, transCaseDescription.GetID());
                    _court.InitializeCourt(tmpCaseDescription, transCaseDescription);
                }
                catch (Exception e)
                {
                    _court.InitializeCourt(transCaseDescription, transCaseDescription);
                    Debug.LogWarning("Translation failed:" + e.Message);
                }
            }
            
        }
        else
        {
            
            try
            {
                string response = await _apiManager.RequestTranslation(transCaseDescription.GetJsonDescription(), seed: seed);
                CaseDescription tmpCaseDescription = JsonConvert.DeserializeObject<CaseDescription>(response);
                _saveManager.SaveCaseDescription(new []{tmpCaseDescription, transCaseDescription});
                _court.InitializeCourt(tmpCaseDescription, transCaseDescription);
            }
            catch (Exception e)
            {
                _saveManager.SaveCaseDescription(new []{transCaseDescription});
                _court.InitializeCourt(transCaseDescription, transCaseDescription);
                Debug.LogWarning("Translation failed:" + e.Message);
            }
            
        }
        
        
        courtPreviewCanvas.SetActive(false);
        Destroy(gameObject);
        
        //try
        //{
        //    string response = await _apiManager.RequestTranslation(transCaseDescription.GetJsonDescription(), seed: _seed);
        //    JSONCaseDescription tmpCaseDescription = JsonConvert.DeserializeObject<JSONCaseDescription>(response);
        //    SaveCaseDescription(new []{tmpCaseDescription, transCaseDescription});
        //    _court.InitializeCourt(tmpCaseDescription, transCaseDescription);
        //    courtPreviewCanvas.SetActive(false);
        //    Destroy(gameObject);
        //}
        //catch (Exception e)
        //{
        //    ToggleButtons(true);
        //    _ = OnError("Case Translation failed, retry");
        //    Debug.LogWarning("Json deserialization failed: " + e.Message);
        //    _seed = Random.Range(0, int.MaxValue);
        //}
        
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
        var tmpDescription = await _apiManager.RequestCaseDescription(prefInputField.text, true, seed);
        if (!string.IsNullOrWhiteSpace(tmpDescription.summary))
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
    }

    private void ToggleButtons(bool enable)
    {
        playButton.interactable = enable;
        newCaseButton.interactable = enable;
        foreach (var item in arrowButtons)
            item.enabled = enable;
    }
    
}
