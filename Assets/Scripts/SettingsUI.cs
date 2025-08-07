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
        if (court != null)
        {
            CaseDescription original = court.GetCaseDescription();
            CaseDescription translated = court.GetTranslatedDescription();

            SaveSystem saveSystem = FindFirstObjectByType<SaveSystem>();
            if (saveSystem != null)
            {
                saveSystem.SaveCaseDescription(new CaseDescription[] { original, translated });
            }
        }

        SceneManager.LoadScene("Menu");
    }


}
