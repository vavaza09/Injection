using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Core.Logging;

namespace Game.Rooms
{
    /// <summary>
    /// Session-scoped (DontDestroyOnLoad) authority for room transitions. Sole owner of
    /// "place the persistent player on room arrival". Knows nothing about saving or
    /// reviving — callers hook those via the <c>onArrived</c> callback / RoomEntered event.
    /// </summary>
    public class RoomManager : MonoBehaviour, IRoomLoader
    {
        private RoomCatalog _catalog;
        private Player _player;
        private Core.Logging.ILogger _logger;

        private bool _isTransitioning;

        public string CurrentRoomId { get; private set; }
        public bool IsTransitioning => _isTransitioning;
        public event Action<string, string> RoomEntered;

        [Inject]
        public void Construct(RoomCatalog catalog, Player player, LoggerFactory loggerFactory)
        {
            _catalog = catalog;
            _player = player;
            _logger = loggerFactory?.CreateLogger("RoomManager");
        }

        public void LoadRoom(string roomId, string arrivalSpawnPointId, Action onArrived = null)
        {
            if (_isTransitioning)
            {
                _logger?.LogWarning($"[RoomManager] Ignoring LoadRoom('{roomId}') — transition already running.");
                return;
            }

            if (_catalog == null || !_catalog.TryGet(roomId, out var def))
            {
                _logger?.LogError($"[RoomManager] Unknown roomId '{roomId}' — cannot load.");
                return;
            }

            StartCoroutine(LoadRoutine(def, arrivalSpawnPointId, onArrived));
        }

        private IEnumerator LoadRoutine(RoomDefinition def, string arrivalSpawnPointId, Action onArrived)
        {
            _isTransitioning = true;
            Debug.Log($"[DIAG] RoomManager.LoadRoutine: loading '{def.sceneName}' ScreenFader={ScreenFader.Instance != null}");

            if (ScreenFader.Instance != null)
            {
                bool faded = false;
                ScreenFader.Instance.FadeOut(() => faded = true);
                Debug.Log("[DIAG] RoomManager: waiting for fade...");
                yield return new WaitUntil(() => faded);
                Debug.Log("[DIAG] RoomManager: fade complete, starting scene load");
            }

            var load = SceneManager.LoadSceneAsync(def.sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                _logger?.LogError($"[RoomManager] Scene '{def.sceneName}' failed to load (not in Build Settings?).");
                _isTransitioning = false;
                ScreenFader.Instance?.FadeIn();
                yield break;
            }

            yield return load;
            // Let the new scene's Awake/Start and its child LifetimeScope build before placing the player.
            yield return null;

            CurrentRoomId = def.roomId;
            PlacePlayer(arrivalSpawnPointId);
            CameraController.instance?.SetTarget(_player.transform);
            if (CameraManager.instance != null)
                CameraManager.instance.SetFollowTarget(_player.transform);
            else
            {
                var vcam = FindFirstObjectByType<CinemachineCamera>();
                if (vcam != null) vcam.Follow = _player.transform;
            }

            onArrived?.Invoke();
            RoomEntered?.Invoke(def.roomId, arrivalSpawnPointId);

            ScreenFader.Instance?.FadeIn();
            _isTransitioning = false;
        }

        private void PlacePlayer(string spawnPointId)
        {
            if (_player == null) return;

            var points = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in points)
            {
                if (p.Id == spawnPointId)
                {
                    _player.transform.position = p.Position;
                    return;
                }
            }

            _logger?.LogWarning($"[RoomManager] SpawnPoint '{spawnPointId}' not found in room '{CurrentRoomId}'.");
        }
    }
}
