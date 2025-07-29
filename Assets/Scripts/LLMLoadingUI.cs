using UnityEngine;
using TMPro;
using System.Collections;

public class LLMLoadingUI : MonoBehaviour
{
    [SerializeField] private LLMUnity.LLM llm;
    [SerializeField] private TMP_Text loadingText;

    private Coroutine pollingCoroutine;

    private void Start()
    {
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
        }

        if (llm.started)
        {
            loadingText.text = "LLM avviato con successo!";
        }
        else if (llm.failed)
        {
            loadingText.text = "Errore con l'avvio del servizio LLM";
        }
     
    }
}