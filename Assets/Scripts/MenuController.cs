using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Pannelli")]
    public GameObject characterPanel;
    public GameObject optionsPanel;

    [Header("Bottoni")]
    public Button newGameButton;
    public Button characterButton;
    public Button leaveGameButton;
    public Button optionsButton;

    private enum OpenPanel
    {
        None,
        Character,
        Options
    }

    private OpenPanel currentOpenPanel = OpenPanel.None;

    private void Start()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        newGameButton.onClick.AddListener(StartNewGame);
        characterButton.onClick.AddListener(OpenCharacterPanel);
        optionsButton.onClick.AddListener(OpenOptionsPanel);
        leaveGameButton.onClick.AddListener(OnLeaveGameClicked);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentOpenPanel != OpenPanel.None)
            {
                CloseCurrentPanel();
            }
            else
            {
                QuitGame();
            }
        }
    }

    public void StartNewGame()
    {
        SceneManager.LoadScene("Scene");
    }

    public void OpenCharacterPanel()
    {
        characterPanel.SetActive(true);
        currentOpenPanel = OpenPanel.Character;
    }

    public void OpenOptionsPanel()
    {
        optionsPanel.SetActive(true);
        currentOpenPanel = OpenPanel.Options;
    }

    private void CloseCurrentPanel()
    {
        switch (currentOpenPanel)
        {
            case OpenPanel.Character:
                if (characterPanel != null) characterPanel.SetActive(false);
                break;
            case OpenPanel.Options:
                if (optionsPanel != null) optionsPanel.SetActive(false);
                break;
        }
        currentOpenPanel = OpenPanel.None;
    }

    private void OnLeaveGameClicked()
    {
        if (currentOpenPanel == OpenPanel.None)
        {
            QuitGame();
        }
        else
        {
            CloseCurrentPanel();
        }
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }
}
