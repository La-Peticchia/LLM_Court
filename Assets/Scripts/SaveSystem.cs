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
    private List<JSONCaseDescription[]> _savedDescriptions;
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
    
    
    public void SaveCaseDescription(JSONCaseDescription[] descriptions)
    {
        int id = PlayerPrefs.GetInt("caseFileID");
        PlayerPrefs.SetInt("caseFileID", id + 1);
        File.WriteAllText(Path.Combine(_saveFilesPath, $"case{id}.json"), JsonConvert.SerializeObject(descriptions, Formatting.Indented));
    }

    public void SaveCaseDescription(JSONCaseDescription[] descriptions, int fileID)
    {
        PlayerPrefs.SetInt("caseFileID", fileID);
        File.WriteAllText(Path.Combine(_saveFilesPath, $"case{fileID}.json"), JsonConvert.SerializeObject(descriptions, Formatting.Indented));

    }

    private void GetSavedDescriptions()
    {
        _savedDescriptions = new List<JSONCaseDescription[]>();
        string[] descriptions = Directory.GetFiles(_saveFilesPath, "*.json");
        
        if (descriptions.Length == 0) return;
        
        savedCasesGroup.SetActive(true);
        
        foreach (string description in descriptions)
        {
            
            JSONCaseDescription[] tmpDescription = JsonConvert.DeserializeObject<JSONCaseDescription[]>(File.ReadAllText(description));
            for (int i = 0; i < tmpDescription.Length; i++)
                tmpDescription[i].SetID(int.Parse(Regex.Match(description, @"\d+").Value));
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

    public JSONCaseDescription[] GetSavedDescriptionsByID(int ID)
    {
        return _savedDescriptions.FirstOrDefault(x => x[0].GetID() == ID);
    }
    
    
}
