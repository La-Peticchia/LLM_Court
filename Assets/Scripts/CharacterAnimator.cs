using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Fixed Prefabs")]
    public GameObject judgePrefab; // Always male

    [Header("Prosecutor Prefabs - Gender Based")]
    public List<GameObject> maleProsecutorPrefabs;
    public List<GameObject> femaleProsecutorPrefabs;

    [Header("Witness Prefabs - Gender Based")]
    public List<GameObject> maleWitnessPrefabs;
    public List<GameObject> femaleWitnessPrefabs;

    private GameObject defensePrefab;

    [Header("UI")]
    public Transform parentCanvas;
    public GameObject aiImageObject;
    public Text aiText;

    private GameObject currentCharacterGO;
    private Animator currentAnimator;
    private string currentRole;

    private Dictionary<string, GameObject> roleToPrefab = new();
    private Dictionary<string, string> characterGenders = new(); // Track assigned genders

    // Public property to get prosecutor gender
    public string ProsecutorGender { get; private set; } = "M";

    private void Awake()
    {
        LoadDefensePrefab();
    }

    private void LoadDefensePrefab()
    {
        string savedName = PlayerPrefs.GetString("SelectedDefenseCharacter", "");
        if (!string.IsNullOrEmpty(savedName))
        {
            GameObject loadedPrefab = Resources.Load<GameObject>("Prefab/" + savedName);
            if (loadedPrefab != null)
                defensePrefab = loadedPrefab;
            else
                Debug.LogWarning("Defense prefab not found: " + savedName);
        }
    }

    /// <summary>
    /// Assegna prefab dinamici basati sui generi
    /// </summary>
    public void AssignDynamicPrefabs(List<string> witnessNames, List<string> witnessGenders, string attackRole)
    {
        roleToPrefab.Clear();
        characterGenders.Clear();

        // Assign prosecutor with random gender
        AssignProsecutorPrefab(attackRole);

        // Assign witnesses based on their specific genders
        AssignWitnessPrefabs(witnessNames, witnessGenders);

        // Judge is always male
        characterGenders["Judge"] = "M";
        characterGenders["Defense"] = "M"; // Default, but won't be used for TTS

        //Debug.Log("Character assignments completed:");
        /*foreach (var kvp in characterGenders)
        {
            Debug.Log($"- {kvp.Key}: {kvp.Value}");
        }*/
    }

    private void AssignProsecutorPrefab(string attackRole)
    {
        // Random gender selection for prosecutor
        bool isMale = Random.Range(0, 2) == 0;
        ProsecutorGender = isMale ? "M" : "F";

        List<GameObject> prosecutorPrefabs = isMale ? maleProsecutorPrefabs : femaleProsecutorPrefabs;

        if (prosecutorPrefabs.Count > 0)
        {
            var randomProsecutor = prosecutorPrefabs[Random.Range(0, prosecutorPrefabs.Count)];
            roleToPrefab[attackRole] = randomProsecutor;
            characterGenders[attackRole] = ProsecutorGender;

            //Debug.Log($"Assigned {(isMale ? "male" : "female")} prosecutor: {randomProsecutor.name}");
        }
        else
        {
            Debug.LogWarning($"No {(isMale ? "male" : "female")} prosecutor prefabs available");
        }
    }

    private void AssignWitnessPrefabs(List<string> witnessNames, List<string> witnessGenders)
    {
        for (int i = 0; i < witnessNames.Count; i++)
        {
            string witnessName = witnessNames[i];

            // Use provided gender or default to male
            string gender = i < witnessGenders.Count ? witnessGenders[i].Trim().ToUpper() : "M";

            // Normalize gender
            if (gender != "M" && gender != "F")
            {
                gender = "M"; // Default to male if invalid
                Debug.LogWarning($"Invalid gender for {witnessName}, defaulting to Male");
            }

            List<GameObject> availablePrefabs = gender == "M" ? maleWitnessPrefabs : femaleWitnessPrefabs;

            if (availablePrefabs.Count > 0)
            {
                var randomWitness = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
                roleToPrefab[witnessName] = randomWitness;
                characterGenders[witnessName] = gender;

                //Debug.Log($"Assigned {witnessName} ({gender}): {randomWitness.name}");
            }
            else
            {
                Debug.LogWarning($"No {(gender == "M" ? "male" : "female")} witness prefabs available");
            }
        }
    }

    /// <summary>
    /// Get the assigned gender for a character
    /// </summary>
    public string GetCharacterGender(string characterName)
    {
        if (characterGenders.TryGetValue(characterName, out string gender))
            return gender;

        // Default fallbacks
        if (characterName == "Judge") return "M";
        if (characterName == "Defense") return "M";
        if (characterName == "Prosecutor") return ProsecutorGender;

        return "M"; // Default to male
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

        GameObject prefabToUse = GetPrefabForRole(role);

        if (prefabToUse == null)
        {
            Debug.LogWarning($"No prefab assigned for role: {role}");
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

    private GameObject GetPrefabForRole(string role)
    {
        if (role == "Judge")
            return judgePrefab;
        else if (role == "Defense")
            return defensePrefab;
        else if (roleToPrefab.ContainsKey(role))
            return roleToPrefab[role];

        return null;
    }

    private bool IsAttackRole(string role)
    {
        return roleToPrefab.ContainsKey(role) &&
               (maleProsecutorPrefabs.Contains(roleToPrefab[role]) ||
                femaleProsecutorPrefabs.Contains(roleToPrefab[role]));
    }

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

    /// <summary>
    /// Get all character gender assignments for external use
    /// </summary>
    public Dictionary<string, string> GetAllCharacterGenders()
    {
        return new Dictionary<string, string>(characterGenders);
    }
}