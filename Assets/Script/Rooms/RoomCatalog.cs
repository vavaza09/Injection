using System.Collections.Generic;
using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// Data-driven registry of rooms. Decouples save data + portals from raw scene
    /// names: callers reference a stable <c>roomId</c>, and the catalog resolves the
    /// scene to load. Lookup is pure (no engine state) so it is unit-testable.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomCatalog", menuName = "Injection/Room Catalog")]
    public class RoomCatalog : ScriptableObject
    {
        [Tooltip("Room loaded on a fresh start (no save).")]
        [SerializeField] private string defaultRoomId;
        [SerializeField] private string defaultSpawnPointId;
        [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

        public string DefaultRoomId => defaultRoomId;
        public string DefaultSpawnPointId => defaultSpawnPointId;

        public bool TryGet(string roomId, out RoomDefinition definition)
        {
            if (!string.IsNullOrEmpty(roomId))
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    var def = rooms[i];
                    if (def != null && def.roomId == roomId)
                    {
                        definition = def;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetSceneName(string roomId, out string sceneName)
        {
            if (TryGet(roomId, out var def))
            {
                sceneName = def.sceneName;
                return true;
            }

            sceneName = null;
            return false;
        }
    }
}
