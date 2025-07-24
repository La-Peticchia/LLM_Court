using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Prefab Fisso")]
    public GameObject judgePrefab;

    [Header("Roster Random")]
    public List<GameObject> attackPrefabs;
    public List<GameObject> witnessPrefabs;

    
    private GameObject defensePrefab;

    [Header("UI")]
    public Transform parentCanvas;
    public GameObject aiImageObject;
    public Text aiText;

    private GameObject currentCharacterGO;
    private Animator currentAnimator;
    private string currentRole;

    private Dictionary<string, GameObject> roleToPrefab = new();

    private void Awake()
    {
        string savedName = PlayerPrefs.GetString("SelectedDefenseCharacter", "");
        if (!string.IsNullOrEmpty(savedName))
        {
            // Carica il prefab dal Resources (o alternativamente da un array)
            GameObject loadedPrefab = Resources.Load<GameObject>("Prefab/" + savedName);
            if (loadedPrefab != null)
                defensePrefab = loadedPrefab;
            else
                Debug.LogWarning("Prefab difesa non trovato: " + savedName);
        }
    }

    public void AssignDynamicPrefabs(List<string> witnesses, string attackRole)
    {
        roleToPrefab.Clear();

        if (attackPrefabs.Count > 0)
        {
            var randomAttack = attackPrefabs[Random.Range(0, attackPrefabs.Count)];
            roleToPrefab[attackRole] = randomAttack;
        }

        foreach (var witness in witnesses)
        {
            if (witnessPrefabs.Count > 0)
            {
                var randomWitness = witnessPrefabs[Random.Range(0, witnessPrefabs.Count)];
                roleToPrefab[witness] = randomWitness;
            }
        }
    }

    public void HideCurrentCharacter()
    {
        if (currentCharacterGO != null)
        {
            aiImageObject.SetActive(false);
            aiText.text = "";

            bool isAttack = IsAttackRole(currentRole);
            bool isDefense = IsDefenseRole(currentRole);

            if (isAttack)
                currentAnimator.SetBool("isExitingAttack", true);
            else if (isDefense)
                currentAnimator.SetBool("isExitingDefense", true);
            else
                currentAnimator.SetBool("isExiting", true);

            StartCoroutine(DestroyAfterExit(currentCharacterGO, isAttack, isDefense));

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
            HideCurrentCharacter();

        currentRole = role;

        GameObject prefabToUse = null;
        if (role == "Judge")
            prefabToUse = judgePrefab;
        else if (role == "Defense")
            prefabToUse = defensePrefab;
        else if (roleToPrefab.ContainsKey(role))
            prefabToUse = roleToPrefab[role];

        if (prefabToUse == null)
        {
            Debug.LogWarning($"Nessun prefab assegnato per il ruolo: {role}");
            aiImageObject.SetActive(true);
            aiText.text = text;
            return;
        }

        currentCharacterGO = Instantiate(prefabToUse, parentCanvas);
        currentAnimator = currentCharacterGO.GetComponent<Animator>();

        if (currentAnimator != null)
        {
            bool isAttack = IsAttackRole(role);
            bool isDefense = IsDefenseRole(role);

            if (isAttack)
                currentAnimator.SetBool("isEnteringAttack", true);
            else if (isDefense)
                currentAnimator.SetBool("isEnteringDefense", true);
            else
                currentAnimator.SetBool("isEntering", true);

            StartCoroutine(ShowTextAfterEnter(currentAnimator, text, isAttack, isDefense));
        }
        else
        {
            aiText.text = text;
            aiImageObject.SetActive(true);
        }
    }

    private bool IsAttackRole(string role) => roleToPrefab.ContainsKey(role) && attackPrefabs.Contains(roleToPrefab[role]);
    private bool IsDefenseRole(string role) => role == "Defense";

    private IEnumerator ShowTextAfterEnter(Animator animator, string text, bool isAttack, bool isDefense)
    {
        string animName = isAttack ? "EnterAttack" : isDefense ? "EnterDefense" : "Enter";
        float waitTime = 0f;

        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(animName) && waitTime < 1f)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        waitTime = 0f;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f && waitTime < 2f)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        if (isAttack)
            animator.SetBool("isEnteringAttack", false);
        else if (isDefense)
            animator.SetBool("isEnteringDefense", false);
        else
            animator.SetBool("isEntering", false);

        aiImageObject.SetActive(!isDefense);
        aiText.text = "";
    }

    private IEnumerator DestroyAfterExit(GameObject obj, bool isAttack, bool isDefense)
    {
        Animator animator = obj.GetComponent<Animator>();
        string animName = isAttack ? "ExitAttack" : isDefense ? "ExitDefense" : "Exit";

        if (animator != null)
        {
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(animName))
                yield return null;
            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;
        }

        Destroy(obj);
    }
}