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
    [SerializeField] private TextMeshProUGUI casePreviewTextbox;
    [SerializeField] private TextMeshProUGUI errorTextbox;
    [SerializeField] private GameObject courtPreviewCanvas;
    

    private APIInterface _apiManager;
    private CaseDescription _caseDescription, _translatedDescription;
    
    private Court _court;

    private void Awake()
    {
        _apiManager = FindFirstObjectByType<APIInterface>();
        _court = FindFirstObjectByType<Court>();
        
        playButton.onClick.AddListener(OnPlayButtonClicked);
        newCaseButton.onClick.AddListener(OnNewCaseButtonClicked);
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        playButton.interactable = false;
        newCaseButton.interactable = false;
        
        //while (string.IsNullOrWhiteSpace(((_caseDescription, _translatedDescription) = await _apiManager.Request())._caseDescription.summary)) {Debug.LogWarning("Empty case description");};
        //casePreviewTextbox.text = _translatedDescription.GetBriefDescription(true);
        
        CaseDescription tmpDesc1, tmpDesc2;
        (tmpDesc1, tmpDesc2) = await _apiManager.Request(prefInputField.text);
        
        if (!string.IsNullOrWhiteSpace(tmpDesc1.summary))
        {
            (_caseDescription, _translatedDescription) = (tmpDesc1, tmpDesc2);
            casePreviewTextbox.text = _translatedDescription.GetBriefDescription(true);
            playButton.interactable = true;
        }
        else
            _ = OnError();
        
        newCaseButton.interactable = true;
    }

    private void OnPlayButtonClicked()
    {
        _court.InitializeCourt(_caseDescription, _translatedDescription);
        courtPreviewCanvas.SetActive(false);
        Destroy(gameObject);
    }
    
    private async void OnNewCaseButtonClicked()
    {
        playButton.interactable = false;
        newCaseButton.interactable = false;
        
        CaseDescription tmpDesc1, tmpDesc2;
        (tmpDesc1, tmpDesc2) = await _apiManager.Request(prefInputField.text);
        

        if (!string.IsNullOrWhiteSpace(tmpDesc1.summary))
        {
            (_caseDescription, _translatedDescription) = (tmpDesc1, tmpDesc2);
            casePreviewTextbox.text = _translatedDescription.GetBriefDescription(true);
            playButton.interactable = true;
        }
        else
           _ = OnError();
        
        newCaseButton.interactable = true;
        
    }

    private async Task OnError()
    {
        errorTextbox.enabled = true;
        await Task.Delay(5000);
        errorTextbox.enabled = false;
    }    
    
}
