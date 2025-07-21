using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class EndGameUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button restartButton;
    //[SerializeField] private Button mainMenuButton;
    [SerializeField] private float typingSpeed = 0.05f;

    private void Start()
    {
        panel.SetActive(false);
        restartButton.onClick.AddListener(RestartGame);
        //mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    public void Show(string message, Color color)
    {
        Debug.Log($"[VERDETTO] Risultato: {message}");
        Time.timeScale = 0f; // Pausa gioco
        panel.SetActive(true);
        resultText.text = "";
        resultText.color = color;
        StartCoroutine(TypeText(message));
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /*private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Assicurati che esista una scena con questo nome
    }*/
}