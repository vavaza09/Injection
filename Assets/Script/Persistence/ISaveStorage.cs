namespace Game.Persistence
{
    public interface ISaveStorage
    {
        bool Exists();
        SaveData Read();
        void Write(SaveData data);
        void Clear();
    }
}
