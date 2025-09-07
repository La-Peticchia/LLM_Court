using UnityEngine;
using TMPro;
using System.Collections;

public class LLMLoadingUI : MonoBehaviour
{
    [SerializeField] private LLMUnity.LLM llm;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private GameObject loadingCanvas;
    private Coroutine pollingCoroutine;
    private CaseGeneration caseGeneration;
    private Court court;
    private bool isRetryCase = false;

    private void Start()
    {
        caseGeneration = FindFirstObjectByType<CaseGeneration>();
        court = FindFirstObjectByType<Court>();

        isRetryCase = CaseMemory.HasValidSavedCase || CaseMemory.RestartingSameCase;

        if (loadingCanvas != null)
            loadingCanvas.SetActive(true);

        pollingCoroutine = StartCoroutine(UpdateStatusText());
    }

    private IEnumerator UpdateStatusText()
    {
        string baseText = "Loading LLM";
        int dotCount = 0;

        while (!llm.started && !llm.failed)
        {
            loadingText.text = baseText + new string('.', dotCount % 4);
            dotCount++;
            yield return new WaitForSeconds(0.5f);

            if (caseGeneration != null)
            {
                caseGeneration.returnToMenuButton.interactable = false;
            }
        }

        if (llm.failed)
        {
            loadingText.text = "Error starting LLM service";
            yield break;
        }

        loadingText.text = "LLM started successfully!";

        
        if (isRetryCase)
        {
            yield return new WaitForSeconds(1f);

            baseText = "Initializing Court";
            dotCount = 0;

            
            while (court == null || !court.enabled || !IsCourtReadyForGameplay())
            {
                if (court == null)
                    court = FindFirstObjectByType<Court>();

                loadingText.text = baseText + new string('.', dotCount % 4);
                dotCount++;
                yield return new WaitForSeconds(0.3f);
            }

            loadingText.text = "Court initialized!";
            yield return new WaitForSeconds(0.5f);

            baseText = "Starting gameplay";
            dotCount = 0;

            
            float waitTime = 0f;
            while (waitTime < 1f) 
            {
                loadingText.text = baseText + new string('.', dotCount % 4);
                dotCount++;
                waitTime += 0.3f;
                yield return new WaitForSeconds(0.3f);
            }
        }

        
        loadingText.text = "Ready!";
        yield return new WaitForSeconds(0.3f);

        if (loadingCanvas != null)
            loadingCanvas.SetActive(false);

        if (caseGeneration != null)
            caseGeneration.returnToMenuButton.interactable = true;
    }

    private bool IsCourtReadyForGameplay()
    {
        if (court == null) return false;
        return court.enabled && court.nextButton != null;
    }
}