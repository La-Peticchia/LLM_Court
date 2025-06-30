using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Prefabs Fissi")]
    public GameObject judgePrefab;

    [Header("Roster Random")]
    public List<GameObject> attackPrefabs;
    public List<GameObject> witnessPrefabs;

    [Header("UI")]
    public Transform parentCanvas;
    public GameObject aiImageObject;
    public Text aiText;

    private GameObject currentCharacterGO;
    private Animator currentAnimator;
    private string currentRole;

    private Dictionary<string, GameObject> roleToPrefab = new();

    public void AssignDynamicPrefabs(List<string> witnesses, string attackRole)
    {
        roleToPrefab.Clear();

        // Attacco → prefab random
        if (attackPrefabs.Count > 0)
            roleToPrefab[attackRole] = attackPrefabs[Random.Range(0, attackPrefabs.Count)];

        // Testimoni → prefab random (diversi o ripetuti)
        foreach (var witness in witnesses)
        {
            if (witnessPrefabs.Count > 0)
                roleToPrefab[witness] = witnessPrefabs[Random.Range(0, witnessPrefabs.Count)];
        }
    }

    public void HideCurrentCharacter()
    {
        if (currentCharacterGO != null)
        {
            aiImageObject.SetActive(false);
            aiText.text = "";

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
            HideCurrentCharacter();
        }

        currentRole = role;

        // Seleziona prefab in base al ruolo
        GameObject prefabToUse = null;
        if (role == "Judge") prefabToUse = judgePrefab;
        else if (roleToPrefab.ContainsKey(role)) prefabToUse = roleToPrefab[role];

        if (prefabToUse == null)
        {
            Debug.LogWarning($"Nessun prefab assegnato per ruolo: {role}");
            aiImageObject.SetActive(true);
            aiText.text = text;
            return;
        }

        currentCharacterGO = Instantiate(prefabToUse, parentCanvas);
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
        // Attende che inizi l’animazione "Enter"
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

        // Attende che termini "Enter"
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
            yield return new WaitForSeconds(0.02f);
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
