using Game.Persistence;

/// <summary>
/// Throwaway ISaveStorage for dev/practice scenes (DevSceneLifetimeScope) — never touches disk, so
/// objective/door state (and everything else backed by SaveService) always starts fresh every Play
/// session. Matches this project's "no Bootstrap, no rooms, no save" convention for dev scenes.
/// </summary>
public sealed class InMemorySaveStorage : ISaveStorage
{
    private SaveData _data;

    public bool Exists() => _data != null;

    public SaveData Read() => _data;

    public void Write(SaveData data) => _data = data;

    public void Clear() => _data = null;
}
