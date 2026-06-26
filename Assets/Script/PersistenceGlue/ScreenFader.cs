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
        if (_group == null)
            _group = BuildFadeCanvas();
        _group.alpha = 0f;
    }

    // Creates a full-screen black overlay Canvas at runtime so ScreenFader works
    // even when the host GameObject has no pre-built Canvas/CanvasGroup child.
    private CanvasGroup BuildFadeCanvas()
    {
        var canvasGO = new GameObject("ScreenFader_Canvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGO.AddComponent<CanvasScaler>();

        var panelGO = new GameObject("BlackPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var rect = panelGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        var img = panelGO.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        var cg = panelGO.AddComponent<CanvasGroup>();
        return cg;
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
