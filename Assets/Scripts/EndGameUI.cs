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

    private void Start()
    {
        panel.SetActive(false);
        returnButton.onClick.AddListener(RestartGame);
        mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    public void Show(string message, Color color)
    {
        Debug.Log($"[VERDETTO] Risultato: {message}");
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMainMenu()
    {
        SceneManager.LoadScene("Menu");
    }
}