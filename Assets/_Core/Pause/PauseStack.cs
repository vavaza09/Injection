using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Pause
{
    /// <summary>
    /// Lazy self-creating singleton (mirrors <see cref="SlowMotion"/>) backing
    /// <see cref="IPauseService"/>. Was added because <c>PauseMenuController</c> and
    /// <c>VictoryScreenController</c> each wrote Time.timeScale directly with a plain bool —
    /// two overlapping pauses (e.g. a cutscene playing under the pause menu) would resume
    /// gameplay the moment either one released, even while the other still wanted it frozen.
    /// </summary>
    public class PauseStack : MonoBehaviour, IPauseService
    {
        private static PauseStack _instance;
        public static PauseStack Instance
        {
            get
            {
                if (_instance == null)
                {
                    var obj = new GameObject("PauseStack");
                    _instance = obj.AddComponent<PauseStack>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        private readonly HashSet<string> _handles = new HashSet<string>();

        public bool IsPaused => _handles.Count > 0;

        /// <summary>Read-only view of which handles currently hold a pause — lets a system
        /// distinguish "I'm the only reason we're paused" from "something else is layered on
        /// top of me" (e.g. <c>ComicPlayer</c> checking whether the pause menu opened over it).</summary>
        public IReadOnlyCollection<string> ActiveHandles => _handles;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Safety net: never let a stale pause handle survive a room transition, regardless
            // of which system pushed it (mirrors SlowMotion's sceneUnloaded reset).
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (_handles.Count == 0) return;
            _handles.Clear();
            Time.timeScale = 1f;
        }

        public void Push(string handle)
        {
            if (this == null || string.IsNullOrEmpty(handle)) return;
            _handles.Add(handle);
            Time.timeScale = 0f;
        }

        public void Release(string handle)
        {
            if (this == null || string.IsNullOrEmpty(handle)) return;
            _handles.Remove(handle);
            if (_handles.Count == 0)
                Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (_instance == this) _instance = null;
        }
    }
}
