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
    private void Start()
    {
        caseGeneration = FindFirstObjectByType<CaseGeneration>();
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
        if (llm.started)
        {
            loadingText.text = "LLM successfully started!";
            if (loadingCanvas != null)
                loadingCanvas.SetActive(false);
            caseGeneration.returnToMenuButton.interactable = true;
        }
        else if (llm.failed)
        {
            loadingText.text = "Error starting LLM service";
        }

    }
}