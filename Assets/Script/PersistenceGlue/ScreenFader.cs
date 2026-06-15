using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float fadeInDuration  = 0.5f;

    private CanvasGroup _group;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _group = GetComponentInChildren<CanvasGroup>();
        if (_group != null)
            _group.alpha = 0f;
    }

    public void FadeOut(System.Action onComplete = null) =>
        StartCoroutine(FadeRoutine(0f, 1f, fadeOutDuration, onComplete));

    public void FadeIn(System.Action onComplete = null) =>
        StartCoroutine(FadeRoutine(1f, 0f, fadeInDuration, onComplete));

    private IEnumerator FadeRoutine(float from, float to, float duration, System.Action onComplete)
    {
        if (_group == null) { onComplete?.Invoke(); yield break; }

        float elapsed = 0f;
        _group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _group.alpha = to;
        onComplete?.Invoke();
    }
}
