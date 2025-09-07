using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class SavePopupUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private Button backButton; 

    private SaveSystem _saveSystem;
    private Court _court;
    private bool _wasLoadedFromContinue;

    public enum ReturnDestination
    {
        MainMenu,
        CourtPreview
    }

    private ReturnDestination _currentDestination;
    private bool _isProcessing = false;

    private void Awake()
    {
        _saveSystem = FindFirstObjectByType<SaveSystem>();
        _court = FindFirstObjectByType<Court>();

        _wasLoadedFromContinue = PlayerPrefs.GetInt("UseLastSavedCase", 0) == 1;

        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(OnNoClicked);
        if (backButton != null) backButton.onClick.AddListener(CancelPopup); 

        popupPanel.SetActive(false);
    }

    // Metodo principale per mostrare il popup di salvataggio
    public void ShowSavePopup(ReturnDestination destination)
    {
        if (_isProcessing) return;

        _currentDestination = destination;

        // MODIFICATO: Se il player ha caricato una partita tramite "Continue" e vuole tornare al menu,
        // aggiorna SOLO il lastCase senza salvare come nuovo caso
        if (_wasLoadedFromContinue && destination == ReturnDestination.MainMenu)
        {
            _ = UpdateLastCaseAndProceed(); // ← NUOVO METODO
            return;
        }

        // Se il caso corrente è già salvato, non mostrare il popup
        if (IsCaseAlreadySaved())
        {
            ProceedWithoutSaving();
            return;
        }

        popupPanel.SetActive(true);
    }

    private bool IsCaseAlreadySaved()
    {
        if (_court == null) return true;

        var currentCase = _court.GetTranslatedDescription();
        return currentCase != null && currentCase.IsSaved();
    }

    private async void OnYesClicked()
    {
        if (_isProcessing) return;

        yesButton.interactable = false;
        noButton.interactable = false;

        await SaveAndProceed();
    }

    private void OnNoClicked()
    {
        if (_isProcessing) return;
        ProceedWithoutSaving();
    }

    private async Task UpdateLastCaseAndProceed()
    {
        _isProcessing = true;
        popupPanel.SetActive(false);

        try
        {
            if (_court != null && _saveSystem != null)
            {
                var originalCase = _court.GetCaseDescription();
                var translatedCase = _court.GetTranslatedDescription();

                if (originalCase != null && translatedCase != null)
                {
                    // Aggiorna SOLO il lastCase, NON salva come nuovo caso
                    await _saveSystem.SaveAsLastCase(new[] { originalCase, translatedCase });
                    Debug.Log("[SAVE_POPUP] Aggiornato solo lastCase per Continue - nessun nuovo salvataggio creato");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante l'aggiornamento lastCase: {e.Message}");
        }

        ProceedToDestination();
    }

    // Salva il caso corrente e va nella scena indicata
    private async Task SaveAndProceed()
    {
        _isProcessing = true;
        popupPanel.SetActive(false);

        try
        {
            if (_court != null && _saveSystem != null)
            {
                var originalCase = _court.GetCaseDescription();
                var translatedCase = _court.GetTranslatedDescription();

                if (originalCase != null && translatedCase != null)
                {
                    // Salva il caso SOLO se non è già salvato
                    if (!translatedCase.IsSaved())
                    {
                        int newId = await _saveSystem.SaveCaseDescription(new[] { originalCase, translatedCase });
                        translatedCase.SetID(newId);
                        originalCase.SetID(newId);
                        Debug.Log("[SAVE_POPUP] Salvato nuovo caso con ID: " + newId);
                    }
                    else
                    {
                        Debug.Log("[SAVE_POPUP] Caso già salvato con ID: " + translatedCase.GetID());
                    }

                    // Salva come ultimo caso per il "Continue"
                    await _saveSystem.SaveAsLastCase(new[] { originalCase, translatedCase });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il salvataggio: {e.Message}");
        }

        ProceedToDestination();
    }

    private void ProceedWithoutSaving()
    {
        _isProcessing = true;
        popupPanel.SetActive(false);
        ProceedToDestination();
    }

    private void ProceedToDestination()
    {
        switch (_currentDestination)
        {
            case ReturnDestination.MainMenu:
                SceneManager.LoadScene("Menu");
                break;

            case ReturnDestination.CourtPreview:
                SceneManager.LoadScene("Scene");
                break;
        }
    }

    public void CancelPopup()
    {
        if (_isProcessing) return;

        popupPanel.SetActive(false);

        yesButton.interactable = true;
        noButton.interactable = true;
    }

    private void Update()
    {
        if (popupPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPopup();
        }
    }
}