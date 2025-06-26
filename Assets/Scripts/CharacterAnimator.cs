using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterAnimator : MonoBehaviour
{
    [System.Serializable]
    public class CharacterPrefab
    {
        public string roleName;
        public GameObject prefab;
    }

    public List<CharacterPrefab> characterPrefabs;
    public Transform parentCanvas;
    public GameObject aiImageObject;
    public Text aiText;

    private GameObject currentCharacterGO;
    private Animator currentAnimator;
    private string currentRole;

    public void HideCurrentCharacter()
    {
        if (currentCharacterGO != null)
        {
            aiImageObject.SetActive(false);
            aiText.text = "";

            // Avvia animazione di uscita
            currentAnimator.SetBool("isExiting", true);
            StartCoroutine(DestroyAfterExit(currentCharacterGO));
            currentCharacterGO = null;
            currentRole = null;
        }
    }

    public void ShowCharacter(string role, string text)
    {
        if (currentRole == role && currentCharacterGO != null)
        {
            aiText.text = text;
            aiImageObject.SetActive(true);
            return;
        }

        if (currentCharacterGO != null)
        {
            HideCurrentCharacter(); // Fa partire animazione di uscita
        }

        currentRole = role;

        CharacterPrefab prefabEntry = characterPrefabs.Find(c => c.roleName == role);
        if (prefabEntry == null)
        {
            Debug.LogWarning($"Prefab per ruolo {role} non trovato!");
            aiImageObject.SetActive(true);
            aiText.text = text;
            return;
        }

        currentCharacterGO = Instantiate(prefabEntry.prefab, parentCanvas);
        currentAnimator = currentCharacterGO.GetComponent<Animator>();
        if (currentAnimator != null)
        {
            currentAnimator.SetBool("isEntering", true);
            StartCoroutine(ShowTextAfterEnter(currentAnimator, text));
        }
        else
        {
            aiText.text = text;
            aiImageObject.SetActive(true);
        }
    }

    private IEnumerator ShowTextAfterEnter(Animator animator, string text)
    {
        // Aspetta che inizi Enter (max 1s)
        float waitTime = 0f;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Enter"))
        {
            waitTime += Time.deltaTime;
            if (waitTime > 1f)
            {
                Debug.LogWarning("Animazione 'Enter' non è partita entro 1s.");
                break;
            }
            yield return null;
        }

        // Aspetta che finisca Enter
        waitTime = 0f;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            waitTime += Time.deltaTime;
            if (waitTime > 2f)
            {
                Debug.LogWarning("Animazione 'Enter' non è finita entro 2s.");
                break;
            }
            yield return null;
        }

        animator.SetBool("isEntering", false);
        aiImageObject.SetActive(true);
        aiText.text = "";

        // Effetto digitazione
        foreach (char c in text)
        {
            aiText.text += c;
            yield return new WaitForSeconds(0.02f);  // velocità di scrittura
        }
    }


    private IEnumerator DestroyAfterExit(GameObject obj)
    {
        Animator animator = obj.GetComponent<Animator>();
        if (animator != null)
        {
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Exit"))
                yield return null;

            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;
        }
        Destroy(obj);
    }
}
