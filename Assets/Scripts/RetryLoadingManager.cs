using UnityEngine;
using TMPro;
using System.Collections;

public class RetryLoadingManager : MonoBehaviour
{
    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private float minimumLoadingTime = 1.5f;

    private static bool isRetryLoading = false;
    private Court court;

    public static void SetRetryLoading()
    {
        isRetryLoading = true;
    }

    private void Start()
    {
        court = FindFirstObjectByType<Court>();

        
        if (isRetryLoading)
        {
            ShowRetryLoading();
            isRetryLoading = false;
        }
    }

    private void ShowRetryLoading()
    {
        if (loadingCanvas != null)
        {
            loadingCanvas.SetActive(true);
            StartCoroutine(RetryLoadingSequence());
        }
    }

    private IEnumerator RetryLoadingSequence()
    {
        string baseText = "Restarting case";
        int dotCount = 0;
        float elapsedTime = 0f;

        
        if (court != null)
            court.enabled = false;

       
        while (elapsedTime < minimumLoadingTime || !IsGameReady())
        {
            if (loadingText != null)
            {
                loadingText.text = baseText + new string('.', dotCount % 4);
                dotCount++;
            }

            yield return new WaitForSeconds(0.3f);
            elapsedTime += 0.3f;
        }
        
        if (loadingCanvas != null)
            loadingCanvas.SetActive(false); 
    }

    private bool IsGameReady()
    {
        if (CaseMemory.HasValidSavedCase)
            return true;

        if (court != null && court.enabled)
            return true;

        return false;
    }
}