using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// Keeps a Screen Space – Camera canvas pointed at the current room's camera.
    /// Re-binds on Start and on every scene load so the correct camera is always set
    /// even though the Main Camera is scene-local (destroyed and recreated each room).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HudCameraBinder : MonoBehaviour
    {
        public static HudCameraBinder Instance { get; private set; }

        private Canvas _canvas;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _canvas = GetComponent<Canvas>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => RebindCamera();

        private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RebindCamera();

        private void RebindCamera()
        {
            var cam = CameraController.instance != null ? CameraController.instance.Cam : Camera.main;
            if (cam != null)
                _canvas.worldCamera = cam;
        }
    }
}
