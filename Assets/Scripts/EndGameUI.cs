using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class EndGameUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button returnButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] private Button retryButton;
    [SerializeField] private SavePopupUI savePopupUI;

    private string savedCaseDescription;
    private string savedTranslatedDescription;


    private void Start()
    {
        panel.SetActive(false);
        returnButton.onClick.AddListener(RestartGame);
        mainMenuButton.onClick.AddListener(GoToMainMenu);
        retryButton.onClick.AddListener(RetrySameCase);;
    }

    public void SaveCase(string original, string translated)
    {
        savedCaseDescription = original;
        savedTranslatedDescription = translated;
    }

    public void Show(string message, Color color)
    {
        Debug.Log($"[VERDETTO] Risultato: {message}");
        panel.SetActive(true);
        resultText.text = "";
        resultText.color = color;
        StartCoroutine(TypeText(message));
    }

    private void RetrySameCase()
    {
        Court court = Object.FindFirstObjectByType<Court>();
        if (court != null)
        {
            CaseMemory.SavedCase = court.GetCaseDescription();
            CaseMemory.SavedTranslatedCase = court.GetTranslatedDescription();
            CaseMemory.RestartingSameCase = true;
            CaseMemory.NewSeed = Random.Range(0, int.MaxValue);
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


    private IEnumerator TypeText(string message)
    {
        foreach (char letter in message)
        {
            resultText.text += letter;
            yield return new WaitForSecondsRealtime(typingSpeed);
        }
    }

    private void RestartGame()
    {
        if (savePopupUI != null)
        {
            panel.SetActive(false);
            savePopupUI.ShowSavePopup(SavePopupUI.ReturnDestination.CourtPreview);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        
    }

    private void GoToMainMenu()
    {
        if (savePopupUI != null)
        {
            panel.SetActive(false);
            savePopupUI.ShowSavePopup(SavePopupUI.ReturnDestination.MainMenu);
        }
        else
        {
            SceneManager.LoadScene("Menu");
        }
    }
}