using UnityEngine;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    [Header("Scelte Personaggio")]
    public GameObject[] defenseChoices; 
    public Button[] selectionButtons;   

    private void Start()
    {
        // Associa ogni bottone a una scelta
        for (int i = 0; i < selectionButtons.Length; i++)
        {
            int index = i; 
            selectionButtons[i].onClick.AddListener(() => SelectCharacter(index));
        }
    }

    private void SelectCharacter(int index)
    {
        PlayerPrefs.SetString("SelectedDefenseCharacter", defenseChoices[index].name);
        PlayerPrefs.Save();

        Debug.Log("Hai selezionato il personaggio: " + defenseChoices[index].name);
    }
}
