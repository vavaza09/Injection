using System.Collections.Generic;

namespace Game.Persistence
{
    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public string spawnPointId;
        public int playerEnergy;
        public List<string> bossesDefeated = new List<string>();
    }
}
