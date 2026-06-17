using Game.Persistence;

namespace Game.Tests.Persistence
{
    public class FakeSaveStorage : ISaveStorage
    {
        private SaveData _stored;

        public int WriteCallCount { get; private set; }
        public int ClearCallCount { get; private set; }

        public bool Exists() => _stored != null;

        public SaveData Read() => _stored;

        public void Write(SaveData data)
        {
            WriteCallCount++;
            _stored = data;
        }

        public void Clear()
        {
            ClearCallCount++;
            _stored = null;
        }
    }
}
