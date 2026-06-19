using UnityEngine;

namespace Game.Spawning
{
    /// <summary>
    /// Seam between "I want an enemy here" and how it is actually created. The default
    /// implementation Instantiates + DI-injects; a pooled implementation can be swapped
    /// in later without touching <see cref="RoomSpawner"/>.
    /// </summary>
    public interface IEnemyFactory
    {
        Enemy Create(GameObject prefab, Vector3 position, Quaternion rotation);
    }
}
