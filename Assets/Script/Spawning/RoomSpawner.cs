using UnityEngine;
using VContainer;
using Core.Logging;
using Game.Rooms;

namespace Game.Spawning
{
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

            var manager = FindAnyObjectByType<RoomObjectiveManager>();

            var markers = FindObjectsByType<EnemySpawnMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int spawned = 0;
            foreach (var marker in markers)
            {
                if (marker.EnemyPrefab == null) continue;

                var objId = marker.ObjectiveId;
                if (!string.IsNullOrEmpty(objId) && manager != null && manager.IsObjectiveComplete(objId))
                    continue;

                var enemy = _factory.Create(marker.EnemyPrefab, marker.Position, marker.Rotation);
                spawned++;

                if (enemy != null && !string.IsNullOrEmpty(objId) && manager != null)
                    manager.RegisterObjectiveEnemy(objId, enemy);
            }

            manager?.SealSpawning();
            _logger?.Log($"[RoomSpawner] Spawned {spawned} enemy(ies) from {markers.Length} marker(s).");
        }
    }
}
