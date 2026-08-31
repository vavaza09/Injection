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

        /// <summary>
        /// Normally (false) dying in this room reloads the scene fresh so RoomSpawner
        /// re-instantiates every EnemySpawnMarker. Tick true only for rooms where a reload
        /// costs more than it gives — e.g. the tutorial, which has no markers at all and
        /// whose TutorialManager would restart from step 1 (SetAbilityGating(true) clears
        /// every unlocked ability). Named "skip" rather than "reload" so the default false
        /// gives every existing room the new behavior automatically, instead of silently
        /// keeping the old one until someone remembers to opt each room in.
        /// </summary>
        public bool skipRoomReloadOnDeath;
    }
}
