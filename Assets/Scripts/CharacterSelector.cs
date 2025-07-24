using UnityEngine;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    [Header("Scelte Personaggio")]
    public GameObject[] defenseChoices; // I prefab tra cui scegliere
    public Button[] selectionButtons;   // I bottoni cliccabili

    private void Start()
    {
        // Associa ogni bottone a una scelta
        for (int i = 0; i < selectionButtons.Length; i++)
        {
            int index = i; // Importante per evitare chiusura errata nella lambda
            selectionButtons[i].onClick.AddListener(() => SelectCharacter(index));
        }
    }

    private void SelectCharacter(int index)
    {
        // Salva il prefab selezionato in PlayerPrefs usando un identificativo univoco (es: il nome)
        PlayerPrefs.SetString("SelectedDefenseCharacter", defenseChoices[index].name);
        PlayerPrefs.Save();

        Debug.Log("Hai selezionato il personaggio: " + defenseChoices[index].name);
    }
}
