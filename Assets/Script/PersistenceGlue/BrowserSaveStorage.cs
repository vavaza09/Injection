using Game.Persistence;
using UnityEngine;

// WebGL save storage: the same JSON as JsonFileSaveStorage, kept in localStorage instead of
// persistentDataPath (IndexedDB), which is keyed by page URL and so lost on every itch.io re-upload.
public sealed class BrowserSaveStorage : ISaveStorage
{
    private readonly string _key;

    public BrowserSaveStorage(string key)
    {
        _key = key;
    }

    public bool Exists() => !string.IsNullOrEmpty(BrowserStorage.Get(_key));

    public SaveData Read()
    {
        string json = BrowserStorage.Get(_key);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Write(SaveData data)
    {
        BrowserStorage.Set(_key, JsonUtility.ToJson(data));
    }

    public void Clear()
    {
        BrowserStorage.Delete(_key);
    }
}
