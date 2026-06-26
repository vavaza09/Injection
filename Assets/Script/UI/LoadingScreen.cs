using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// DontDestroyOnLoad loading overlay shown during scene transitions.
    /// Place this as a prefab/GO in Bootstrap scene.
    ///
    /// Slots (assign in Inspector):
    ///   canvasGroup   — CanvasGroup on the root panel for fade
    ///   spinnerImage  — Image that rotates continuously while loading
    ///   centerIcon    — Static or animated game icon in the center
    ///   centerAnimator — Animator on centerIcon for sprite animation (optional)
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        [Header("Canvas")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Elements")]
        [SerializeField] private Image spinnerImage;
        [SerializeField] private Image centerIcon;
        [SerializeField] private Animator centerAnimator;

        [Header("Transition")]
        [SerializeField] private float fadeInDuration  = 0.25f;
        [SerializeField] private float fadeOutDuration = 0.25f;

        [Header("Spinner")]
        [SerializeField] private float spinnerSpeed = 180f;
        [SerializeField] private bool clockwise = true;

        private bool _spinning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (canvasGroup == null)
                canvasGroup = GetComponentInChildren<CanvasGroup>(includeInactive: true);

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            var canvas = GetComponentInChildren<Canvas>(includeInactive: true);
            if (canvas != null) canvas.sortingOrder = 32767;

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_spinning || spinnerImage == null) return;
            float sign = clockwise ? -1f : 1f;
            spinnerImage.rectTransform.Rotate(0f, 0f, sign * spinnerSpeed * Time.unscaledDeltaTime);
        }

        /// <summary>Fade in the loading screen then call onShown.</summary>
        public void Show(System.Action onShown = null)
        {
            gameObject.SetActive(true);
            _spinning = true;
            if (centerAnimator != null) centerAnimator.enabled = true;
            StartCoroutine(FadeRoutine(0f, 1f, fadeInDuration, onShown));
        }

        /// <summary>Fade out the loading screen then call onHidden.</summary>
        public void Hide(System.Action onHidden = null)
        {
            StartCoroutine(FadeRoutine(1f, 0f, fadeOutDuration, () =>
            {
                _spinning = false;
                if (centerAnimator != null) centerAnimator.enabled = false;
                gameObject.SetActive(false);
                onHidden?.Invoke();
            }));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration, System.Action onComplete)
        {
            if (canvasGroup == null) { onComplete?.Invoke(); yield break; }

            canvasGroup.alpha = from;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            canvasGroup.alpha = to;
            onComplete?.Invoke();
        }
    }
}
