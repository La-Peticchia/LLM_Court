using System.IO;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string sceneName;      
    public int round;             
    public bool gameFinished;

    public string caseDescription;
    public string translatedDescription;   
}

public static class GameSaveSystem
{
    public static bool IsContinue { get; set; } = false;

    private static string SavePath => Application.persistentDataPath + "/save.json";

    public static void SaveGame(
        string sceneName = "Scene",
        int round = 0,
        bool finished = false,
        string caseDescription = "",
        string translatedDescription = "")
    {
        SaveData data = new SaveData
        {
            sceneName = sceneName,
            round = round,
            gameFinished = finished,
            caseDescription = caseDescription,
            translatedDescription = translatedDescription
        };

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(SavePath, json);//sovrascrive quello precedente
        Debug.Log("Gioco salvato in: " + SavePath);
    }

    public static SaveData LoadGame()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        return null;
    }

    public static bool HasSavedGame()
    {
        if (!File.Exists(SavePath)) return false;
        var data = LoadGame();
        return data != null && !data.gameFinished;
    }

    public static void ClearSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }
}
