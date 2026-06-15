using System.IO;
using UnityEngine;
using Game.Persistence;

public class JsonFileSaveStorage : ISaveStorage
{
    private readonly string _filePath;

    public JsonFileSaveStorage(string fileName)
    {
        _filePath = Path.Combine(Application.persistentDataPath, fileName);
    }

    public bool Exists() => File.Exists(_filePath);

    public SaveData Read()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            string json = File.ReadAllText(_filePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Write(SaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
