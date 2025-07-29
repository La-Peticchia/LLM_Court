using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private GameObject settingsUI;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Court court;

    private void Start()
    {
        settingsUI.SetActive(false);
        settingsButton.onClick.AddListener(OpenSettings);
        returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void Update()
    {
        if (settingsUI.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSettings();
        }
    }

    public void OpenSettings()
    {
        settingsUI.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsUI.SetActive(false);
    }

    private void ReturnToMainMenu()
    {
        StartCoroutine(SaveAndReturn());
    }

    private IEnumerator SaveAndReturn()
    {
        if (court == null)
            court = FindFirstObjectByType<Court>();

        if (court != null)
        {
            GameSaveSystem.SaveGame(
                sceneName: SceneManager.GetActiveScene().name,
                round: court.GetCurrentRound(),
                finished: false,
                caseDescription: court.GetCaseDescription().GetTotalDescription(false),
                translatedDescription: court.GetTranslatedDescription().GetTotalDescription(false)
            );
        }
        else
        {
            Debug.LogWarning("Court non trovato per salvare il gioco.");
        }

        // Aspetta un frame per garantire che il salvataggio vada a buon fine
        yield return null;

        SceneManager.LoadScene("Menu");
    }

}
