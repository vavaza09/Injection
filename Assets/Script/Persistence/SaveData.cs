using System.Collections.Generic;

namespace Game.Persistence
{
    /// <summary>Death-respawn anchor: written at save points, restores position + energy + heals.</summary>
    [System.Serializable]
    public class CheckpointAnchor
    {
        public string roomId;
        public string spawnPointId;
        public int energy;
    }

    /// <summary>Continue anchor: written on every room entry, drives "resume where I left off".</summary>
    [System.Serializable]
    public class LastRoomAnchor
    {
        public string roomId;
        public string spawnPointId;
    }

    /// <summary>
    /// v4 schema. Two anchors: <see cref="checkpoint"/> (death-respawn, set by save points) and
    /// <see cref="lastRoom"/> (continue-from-menu, set by autosave-on-entry). Stays UnityEngine-free
    /// (asmdef noEngineReferences) so it can be unit-tested without the engine. JsonUtility-compatible.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int version = 4;
        public CheckpointAnchor checkpoint = new CheckpointAnchor();
        public LastRoomAnchor lastRoom = new LastRoomAnchor();
        public List<string> bossesDefeated = new List<string>();
        public List<string> objectivesCompleted = new List<string>();
        public List<string> comicsSeen = new List<string>();
    }
}
