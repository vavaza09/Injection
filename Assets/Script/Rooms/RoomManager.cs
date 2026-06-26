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

        [SerializeField] private float holdBlackDuration = 0.15f;

        private IEnumerator LoadRoutine(RoomDefinition def, string arrivalSpawnPointId, Action onArrived)
        {
            _isTransitioning = true;

            // Fade in the loading screen if available, otherwise fall back to plain black fade.
            bool useLoadingScreen = Game.UI.LoadingScreen.Instance != null;
            if (useLoadingScreen)
            {
                bool shown = false;
                Game.UI.LoadingScreen.Instance.Show(() => shown = true);
                yield return new WaitUntil(() => shown);
            }
            else if (ScreenFader.Instance != null)
            {
                bool faded = false;
                ScreenFader.Instance.FadeOut(() => faded = true);
                yield return new WaitUntil(() => faded);
            }

            var load = SceneManager.LoadSceneAsync(def.sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                _logger?.LogError($"[RoomManager] Scene '{def.sceneName}' failed to load (not in Build Settings?).");
                _isTransitioning = false;
                if (useLoadingScreen) Game.UI.LoadingScreen.Instance.Hide();
                else ScreenFader.Instance?.FadeIn();
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

            if (holdBlackDuration > 0f)
                yield return new WaitForSecondsRealtime(holdBlackDuration);

            // Fade out the loading screen / fade in the game view.
            if (useLoadingScreen)
            {
                bool hidden = false;
                Game.UI.LoadingScreen.Instance.Hide(() => hidden = true);
                yield return new WaitUntil(() => hidden);
            }
            else if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.FadeIn();
            }

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
