using System;

namespace Game.Rooms
{
    /// <summary>
    /// Orchestrates discrete cross-scene room transitions: fade out, swap the active
    /// scene, place the persistent player at the arrival spawn point, fade in.
    /// </summary>
    public interface IRoomLoader
    {
        /// <summary>Room id currently loaded (null before the first room is entered).</summary>
        string CurrentRoomId { get; }

        /// <summary>True while a transition is mid-flight; portals must ignore re-entry.</summary>
        bool IsTransitioning { get; }

        /// <summary>Raised after the player has been placed in the newly entered room.</summary>
        event Action<string, string> RoomEntered; // (roomId, spawnPointId)

        /// <summary>
        /// Load <paramref name="roomId"/> and place the player at
        /// <paramref name="arrivalSpawnPointId"/>. <paramref name="onArrived"/> runs once
        /// the player is positioned (used by death-respawn to revive, by continue to
        /// restore energy) — keeps revive/save concerns out of the loader itself.
        /// </summary>
        void LoadRoom(string roomId, string arrivalSpawnPointId, Action onArrived = null);
    }
}
