using UnityEngine;
using System.Collections;

public class MuzzleFlashEffect : MonoBehaviour
{
    [Header("Flash (pre-shot warning)")]
    [SerializeField] public float flashDuration = 0.15f;
    [SerializeField] public float flashMaxScaleX = 0.5f;
    [SerializeField] public float flashMaxScaleY = 0.4f;
    [SerializeField] public Color flashStartColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] public Color flashPeakColor  = new Color(1f, 0.95f, 0.35f, 1f);

    [Header("Smoke (at-shot lingering)")]
    [SerializeField] public float smokeDuration    = 0.5f;
    [SerializeField] public float smokeDriftSpeed  = 1.5f;
    [SerializeField] public float smokePuffMaxScale = 0.35f;
    [SerializeField] public Color smokeColor = new Color(0.3f, 0.28f, 0.22f, 0.85f);

    private SpriteRenderer   _flashSr;
    private SpriteRenderer[] _puffSrs;
    private Coroutine        _playCoroutine;

    private void Awake()
    {
        Sprite circle = CreateCircleSprite(32);

        GameObject flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(transform, false);
        _flashSr = flashGo.AddComponent<SpriteRenderer>();
        _flashSr.sprite = circle;
        _flashSr.sortingOrder = 52;
        _flashSr.color = Color.clear;

        _puffSrs = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject puffGo = new GameObject("SmokePuff" + i);
            puffGo.transform.SetParent(transform, false);
            SpriteRenderer sr = puffGo.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.sortingOrder = 50;
            sr.color = Color.clear;
            _puffSrs[i] = sr;
        }

        gameObject.SetActive(false);
    }

    public void Play(Quaternion worldRotation)
    {
        transform.rotation = worldRotation;
        if (_playCoroutine != null)
            StopCoroutine(_playCoroutine);
        gameObject.SetActive(true);
        _playCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // Reset state before animating
        _flashSr.transform.localPosition = Vector3.zero;
        _flashSr.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        _flashSr.color = Color.clear;
        for (int i = 0; i < _puffSrs.Length; i++)
        {
            _puffSrs[i].transform.localPosition = Vector3.zero;
            _puffSrs[i].transform.localScale = Vector3.one * 0.05f;
            _puffSrs[i].color = Color.clear;
        }

        // Phase 1: flash — scales up then fades, total flashDuration seconds
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / flashDuration, 1f);

            // Scale: quick expand (0→0.6t peak) then hold
            float expandT = Mathf.Clamp01(t / 0.6f);
            float sx = Mathf.Lerp(0.1f, flashMaxScaleX, expandT);
            float sy = Mathf.Lerp(0.1f, flashMaxScaleY, expandT);
            _flashSr.transform.localScale = new Vector3(sx, sy, 1f);

            // Color: white→yellow (0→0.4t), then fade (0.5→1t)
            Color col = t < 0.4f
                ? Color.Lerp(flashStartColor, flashPeakColor, t / 0.4f)
                : flashPeakColor;
            col.a = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            _flashSr.color = col;

            yield return null;
        }
        _flashSr.color = Color.clear;

        // Phase 2: smoke puffs drift in fire direction (local +X = rotated fire dir)
        // Parent is already rotated to worldRotation so local right = fire direction.
        Vector2[] localDrifts =
        {
            Vector2.right,
            new Vector2(0.86f,  0.5f),   // 30° above fire axis
            new Vector2(0.86f, -0.5f),   // 30° below fire axis
        };

        t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / smokeDuration, 1f);
            for (int i = 0; i < _puffSrs.Length; i++)
            {
                _puffSrs[i].transform.localPosition +=
                    (Vector3)(localDrifts[i] * smokeDriftSpeed * Time.deltaTime);

                float sc = Mathf.Lerp(0.05f, smokePuffMaxScale, t);
                _puffSrs[i].transform.localScale = Vector3.one * sc;

                Color c = smokeColor;
                c.a = Mathf.Lerp(smokeColor.a, 0f, t);
                _puffSrs[i].color = c;
            }
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - dist / center);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
