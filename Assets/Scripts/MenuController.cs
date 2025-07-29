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
    public Button backFromCharacterButton;
    public Button backFromOptionsButton;
    public Button continueButton;
    public GameObject caseGeneration;


    private void Start()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        newGameButton.onClick.AddListener(StartNewGame);
        characterButton.onClick.AddListener(OpenCharacterPanel);
        leaveGameButton.onClick.AddListener(QuitGame);
        optionsButton.onClick.AddListener(OpenOptionsPanel);

        backFromCharacterButton.onClick.AddListener(CloseCharacterPanel);
        backFromOptionsButton.onClick.AddListener(CloseOptionsPanel);

        continueButton.onClick.AddListener(ContinueGame);
        continueButton.interactable = GameSaveSystem.HasSavedGame(); // Interagibile solo se ha salvataggio
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (characterPanel.activeSelf)
            {
                CloseCharacterPanel();
            }
            else if (optionsPanel.activeSelf)
            {
                CloseOptionsPanel();
            }
        }
    }

    public void StartNewGame()
    {
        SceneManager.LoadScene("Scene");
    }

    private void ContinueGame()
    {
        SaveData data = GameSaveSystem.LoadGame();
        if (data != null)
        {
            GameSaveSystem.IsContinue = true;
            SceneManager.LoadScene(data.sceneName);
            
        }
    }

    public void OpenCharacterPanel()
    {
        characterPanel.SetActive(true);
    }

    public void CloseCharacterPanel()
    {
        characterPanel.SetActive(false);
    }

    public void OpenOptionsPanel()
    {
        optionsPanel.SetActive(true);
    }

    public void CloseOptionsPanel()
    {
        optionsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }
}
