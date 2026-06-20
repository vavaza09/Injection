using System;

namespace Game.Rooms
{
    /// <summary>
    /// Pure data entry mapping a logical room id to the Unity scene that hosts it.
    /// Stored inside <see cref="RoomCatalog"/>. Kept UnityEngine-free so it can be
    /// referenced from data/test code without an engine dependency.
    /// </summary>
    [Serializable]
    public class RoomDefinition
    {
        public string roomId;
        public string sceneName;
        public string displayName;
    }
}
