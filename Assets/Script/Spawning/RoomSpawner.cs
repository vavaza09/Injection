using UnityEngine;
using VContainer;
using Core.Logging;

namespace Game.Spawning
{
    /// <summary>
    /// Per-room (lives in the room scene, injected by its child LifetimeScope). On scene
    /// start it reads every <see cref="EnemySpawnMarker"/> and spawns it via the factory.
    /// Deterministic + stateless: re-entering a room always yields a fresh population.
    /// </summary>
    public class RoomSpawner : MonoBehaviour
    {
        private IEnemyFactory _factory;
        private Core.Logging.ILogger _logger;

        [Inject]
        public void Construct(IEnemyFactory factory, LoggerFactory loggerFactory)
        {
            _factory = factory;
            _logger = loggerFactory?.CreateLogger("RoomSpawner");
        }

        private void Start()
        {
            SpawnAll();
        }

        public void SpawnAll()
        {
            if (_factory == null)
            {
                _logger?.LogError("[RoomSpawner] No IEnemyFactory injected — child scope not wired?");
                return;
            }

            var markers = FindObjectsByType<EnemySpawnMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int spawned = 0;
            foreach (var marker in markers)
            {
                if (marker.EnemyPrefab == null) continue;
                _factory.Create(marker.EnemyPrefab, marker.Position, marker.Rotation);
                spawned++;
            }

            _logger?.Log($"[RoomSpawner] Spawned {spawned} enemy(ies) from {markers.Length} marker(s).");
        }
    }
}
