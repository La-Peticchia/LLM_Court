using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseGeneration : MonoBehaviour
{
    [SerializeField] private Button[] playButtons;
    [SerializeField] private Button newCaseButton;
    [SerializeField] private TextMeshProUGUI prefInputField;
    [SerializeField] private TextMeshProUGUI errorTextbox;
    [SerializeField] private GameObject courtPreviewCanvas;
    
    private APIInterface _apiManager;
    private CourtPreviewAnimation _courtPreviewAnimation;
    
    private CaseDescription[] _caseDescription, _translatedDescription;
    
    private Court _court;
    private CourtRecordUI _courtRecordUI;

    private void Awake()
    {
        _apiManager = FindFirstObjectByType<APIInterface>();
        _court = FindFirstObjectByType<Court>();
        _courtPreviewAnimation = FindFirstObjectByType<CourtPreviewAnimation>();
        _courtRecordUI = FindFirstObjectByType<CourtRecordUI>();

        newCaseButton.onClick.AddListener(OnNewCaseButtonClicked);
        
        _caseDescription = new CaseDescription[2];
        _translatedDescription = new CaseDescription[2];
        _courtRecordUI.isGameplay = false;

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        if (CaseMemory.HasValidSavedCase)
        {
            courtPreviewCanvas.SetActive(false); // Nasconde la preview
            _court.InitializeCourt(CaseMemory.SavedCase.Value, CaseMemory.SavedTranslatedCase.Value);
            CaseMemory.Clear(); // Pulizia dopo riutilizzo
            Destroy(gameObject); // Rimuove il canvas preview
            return;
        }
        StoreDescriptions();
    }
    
    public void OnPlayButtonClicked(int buttonID)
    {
        ToggleButtons(false);

        _court.InitializeCourt(_caseDescription[buttonID], _translatedDescription[buttonID]);
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
        ToggleButtons(false);

        var descriptions = await _apiManager.Request(prefInputField.text);
        if (!string.IsNullOrWhiteSpace(descriptions.Item1.summary))
        {
            if(string.IsNullOrWhiteSpace(_caseDescription[0].summary))
                (_caseDescription[0], _translatedDescription[0]) = descriptions;
            else if(string.IsNullOrWhiteSpace(_caseDescription[1].summary))
                (_caseDescription[1], _translatedDescription[1]) = descriptions;
            else
            {
                (_caseDescription[0], _translatedDescription[0]) = (_caseDescription[1], _translatedDescription[1]);
                (_caseDescription[1], _translatedDescription[1]) = descriptions;
            }            
            
            await _courtPreviewAnimation.PlayAnimation(descriptions.Item2.GetBriefDescription(true));
        }
        else
            _ = OnError();
        
        ToggleButtons(true);
    }

    private void ToggleButtons(bool enable)
    {
        foreach (var item in playButtons)
            item.interactable = enable;
        newCaseButton.interactable = enable;
    }
    
}
