using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSystem : MonoBehaviour
{


    [SerializeField] private GameObject savedCasesGroup;
    [SerializeField] private RectTransform buttonContentBox;
    [SerializeField] private Button buttonPrefab;
    
    
    
    private string _saveFilesPath;
    private List<CaseDescription[]> _savedDescriptions;
    private CaseGeneration _generationManager;
    
    private void Awake()
    {
        _generationManager = FindFirstObjectByType<CaseGeneration>();
        _saveFilesPath = Path.Combine(Application.dataPath, "SavedCases");
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GetSavedDescriptions();
        
    }
    
    
    public void SaveCaseDescription(CaseDescription[] descriptions)
    {
        int id = PlayerPrefs.GetInt("caseFileID");
        PlayerPrefs.SetInt("caseFileID", id + 1);
        File.WriteAllText(Path.Combine(_saveFilesPath, $"case{id}.json"), JsonConvert.SerializeObject(descriptions, Formatting.Indented));

        string lastCasePath = Path.Combine(_saveFilesPath, $"lastCase.json");
        File.WriteAllText(lastCasePath, JsonConvert.SerializeObject(descriptions, Formatting.Indented));
    }

    public void SaveCaseDescription(CaseDescription[] descriptions, int fileID)
    {
        PlayerPrefs.SetInt("caseFileID", fileID);
        File.WriteAllText(Path.Combine(_saveFilesPath, $"case{fileID}.json"), JsonConvert.SerializeObject(descriptions, Formatting.Indented));

        string lastCasePath = Path.Combine(_saveFilesPath, $"lastCase.json");
        File.WriteAllText(lastCasePath, JsonConvert.SerializeObject(descriptions, Formatting.Indented));
    }

    private void GetSavedDescriptions()
    {
        _savedDescriptions = new List<CaseDescription[]>();

        string[] descriptions = Directory.GetFiles(_saveFilesPath, "*.json");

        if (descriptions.Length == 0) return;

        savedCasesGroup.SetActive(true);

        foreach (string description in descriptions)
        {
            // Try-catch per evitare crash se il file è corrotto o malformato
            CaseDescription[] tmpDescription = null;
            try
            {
                tmpDescription = JsonConvert.DeserializeObject<CaseDescription[]>(File.ReadAllText(description));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Errore nella lettura del file {description}: {ex.Message}");
                continue;
            }

            // Controllo su deserializzazione nulla o array vuoto
            if (tmpDescription == null || tmpDescription.Length == 0)
            {
                Debug.LogWarning($"File vuoto o non valido: {description}");
                continue;
            }

            // Regex + controllo speciale per lastCase.json: Estrai l’ID numerico se presente
            var matches = Regex.Matches(description, @"\d+");
            if (matches.Count > 0)
            {
                int id = int.Parse(matches[^1].Value);
                for (int i = 0; i < tmpDescription.Length; i++)
                    tmpDescription[i].SetID(id);
            }
            else if (!description.EndsWith("lastCase.json"))
            {
                // Se non è lastCase.json e non ha ID, lo saltiamo per evitare errori
                Debug.LogWarning($"Nessun ID valido trovato nel nome del file: {description}. Salto questo file.");
                continue;
            }
            else
            {
                // Caso speciale per lastCase.json (non ha numero), assegniamo ID temporaneo
                for (int i = 0; i < tmpDescription.Length; i++)
                    tmpDescription[i].SetID(-1); // o un ID speciale per riconoscerlo se serve
            }

            _savedDescriptions.Add(tmpDescription);

            Button tmpButton = Instantiate(buttonPrefab, buttonContentBox);
            tmpButton.GetComponentInChildren<TextMeshProUGUI>().text = tmpDescription[^1].title;

            tmpButton.onClick.AddListener(() =>
            {
                _generationManager.AddDescriptionToList(tmpDescription[^1]);
                tmpButton.onClick.RemoveAllListeners();
                Destroy(tmpButton.gameObject);
            });
        }
    }


    public bool CheckForTranslation(int ID)
    {
        return _savedDescriptions.Any(x => x.Length == 2 && x[1].GetID() == ID);
    }

    public CaseDescription[] GetSavedDescriptionsByID(int ID)
    {
        return _savedDescriptions.FirstOrDefault(x => x[0].GetID() == ID);
    }

    //carico l'ultimo caso salvato
    public CaseDescription[] GetLastSavedCase()
    {
        string lastCasePath = Path.Combine(_saveFilesPath, "lastCase.json");

        if (File.Exists(lastCasePath))
        {
            return JsonConvert.DeserializeObject<CaseDescription[]>(File.ReadAllText(lastCasePath));
        }

        return null;
    }


}
