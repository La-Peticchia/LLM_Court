using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseGeneration : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button newCaseButton;
    [SerializeField] private TextMeshProUGUI prefInputField;
    [SerializeField] private TextMeshProUGUI errorTextbox;
    [SerializeField] private GameObject courtPreviewCanvas;
    
    private APIInterface _apiManager;
    private CourtPreviewAnimation _courtPreviewAnimation;
    
    private CaseDescription[] _caseDescription, _translatedDescription;
    
    private Court _court;

    private void Awake()
    {
        _apiManager = FindFirstObjectByType<APIInterface>();
        _court = FindFirstObjectByType<Court>();
        _courtPreviewAnimation = FindFirstObjectByType<CourtPreviewAnimation>();
        
        playButton.onClick.AddListener(OnPlayButtonClicked);
        newCaseButton.onClick.AddListener(OnNewCaseButtonClicked);
        
        _caseDescription = new CaseDescription[2];
        _translatedDescription = new CaseDescription[2];
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        StoreDescriptions();
    }

    private void OnPlayButtonClicked()
    {
        _court.InitializeCourt(_caseDescription[1], _translatedDescription[1]);
        courtPreviewCanvas.SetActive(false);
        Destroy(gameObject);
    }
    
    private async void OnNewCaseButtonClicked()
    {
        StoreDescriptions();
    }

    private async Task OnError()
    {
        errorTextbox.enabled = true;
        await Task.Delay(5000);
        errorTextbox.enabled = false;
    }

    private async void StoreDescriptions()
    {
        playButton.interactable = false;
        newCaseButton.interactable = false;
        
        var descriptions = await _apiManager.Request(prefInputField.text);
        if (!string.IsNullOrWhiteSpace(descriptions.Item1.summary))
        {
            (_caseDescription[0], _translatedDescription[0]) = (_caseDescription[1], _translatedDescription[1]);
            (_caseDescription[1], _translatedDescription[1]) = descriptions;
            await _courtPreviewAnimation.PlayAnimation(_translatedDescription[1].GetBriefDescription(true));
        }
        else
            _ = OnError();
        
        if(!string.IsNullOrWhiteSpace(_caseDescription[1].summary))
            playButton.interactable = true;
        
        newCaseButton.interactable = true;
    }
    
}
