using Core.Logging;

namespace Game.Persistence
{
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;
        private readonly ILogger _logger;

        private const int CurrentVersion = 2;

        public SaveService(ISaveStorage storage, ILogger logger)
        {
            _storage = storage;
            _logger  = logger;
        }

        public bool HasSave() => _storage.Exists();

        public SaveData Load()
        {
            if (!_storage.Exists()) return null;

            var data = _storage.Read();
            if (data == null)
            {
                _logger?.LogWarning("[SaveService] Save file corrupted or unreadable.");
                return null;
            }

            if (data.version != CurrentVersion)
            {
                var migrated = Migrate(data);
                if (migrated == null)
                {
                    _logger?.LogWarning($"[SaveService] Save version mismatch (found {data.version}, expected {CurrentVersion}). Discarding.");
                    return null;
                }
                return migrated;
            }

            return data;
        }

        // Seam for future schema migrations. No path defined yet (v1 -> v2 changed shape),
        // so older saves are discarded. Add real migration steps here when needed.
        private SaveData Migrate(SaveData old) => null;

        public void Save(SaveData data)
        {
            if (data == null) return;
            data.version = CurrentVersion;
            _storage.Write(data);
            _logger?.Log($"[SaveService] Saved (checkpoint room '{data.checkpoint?.roomId}', last room '{data.lastRoom?.roomId}').");
        }

        public void Delete()
        {
            _storage.Clear();
            _logger?.Log("[SaveService] Save deleted.");
        }

        public void MarkBossDefeated(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return;

            var data = Load() ?? new SaveData();
            if (!data.bossesDefeated.Contains(bossId))
            {
                data.bossesDefeated.Add(bossId);
                Save(data);
                _logger?.Log($"[SaveService] Boss '{bossId}' marked defeated.");
            }
        }

        public bool IsBossDefeated(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return false;
            var data = Load();
            return data != null && data.bossesDefeated.Contains(bossId);
        }
    }
}
