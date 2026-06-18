using UnityEngine;
using System.Collections;

public class MuzzleFlashEffect : MonoBehaviour
{
    [Header("Flash Settings")]
    [Tooltip("How long the bright flash glow lasts")]
    [SerializeField] private float flashDuration = 0.15f;

    [Tooltip("Max scale of the flash sprite")]
    [SerializeField] private float flashMaxScale = 0.5f;

    [Tooltip("Flash color")]
    [SerializeField] private Color flashColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Header("Smoke Settings")]
    [Tooltip("Number of smoke puffs to spawn")]
    [SerializeField] private int puffCount = 5;

    [Tooltip("How long each smoke puff lasts")]
    [SerializeField] private float puffLifetime = 0.6f;

    [Tooltip("Spread angle of smoke behind the shot direction (degrees)")]
    [SerializeField] private float puffSpreadAngle = 120f;

    [Tooltip("Min launch speed of smoke puffs")]
    [SerializeField] private float puffMinSpeed = 1f;

    [Tooltip("Max launch speed of smoke puffs")]
    [SerializeField] private float puffMaxSpeed = 3f;

    [Tooltip("Smoke puff start color")]
    [SerializeField] private Color puffColor = new Color(0.7f, 0.65f, 0.55f, 0.8f);

    [Tooltip("Min starting scale of each puff")]
    [SerializeField] private float puffMinScale = 0.1f;

    [Tooltip("Max starting scale of each puff")]
    [SerializeField] private float puffMaxScale = 0.3f;

    [Header("Debug")]
    [Tooltip("Check this to play a test flash in edit mode")]
    [SerializeField] private bool testFlash = false;

    private SpriteRenderer _flashSr;
    private Sprite _circleSprite;
    private Coroutine _flashCoroutine;

    private void Awake()
    {
        _circleSprite = CreateCircleSprite(32);

        GameObject flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(transform, false);
        _flashSr = flashGo.AddComponent<SpriteRenderer>();
        _flashSr.sprite = _circleSprite;
        _flashSr.sortingOrder = 52;
        _flashSr.color = Color.clear;
    }

    private void OnValidate()
    {
        if (testFlash && Application.isPlaying)
        {
            PlayFlash(Vector2.right);
            testFlash = false;
        }
    }

    public void PlayFlash(Vector2 direction)
    {
        if (_flashSr == null) return;
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);
        gameObject.SetActive(true);
        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    public void PlaySmoke(Vector2 direction)
    {
        gameObject.SetActive(true);
        StartCoroutine(SmokeRoutine(direction));
    }

    // Legacy combined play — kept for backward compatibility
    public void Play(Quaternion worldRotation)
    {
        Vector2 dir = worldRotation * Vector2.right;
        PlayFlash(dir);
        PlaySmoke(dir);
    }

    private IEnumerator FlashRoutine()
    {
        _flashSr.transform.localPosition = Vector3.zero;
        _flashSr.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        _flashSr.color = Color.clear;

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / flashDuration, 1f);
            float expandT = Mathf.Clamp01(t / 0.6f);
            float s = Mathf.Lerp(0.1f, flashMaxScale, expandT);
            _flashSr.transform.localScale = new Vector3(s, s, 1f);
            Color col = flashColor;
            col.a = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            _flashSr.color = col;
            yield return null;
        }
        _flashSr.color = Color.clear;
    }

    private IEnumerator SmokeRoutine(Vector2 fireDirection)
    {
        int count = Mathf.Max(1, puffCount);
        GameObject[] puffGos = new GameObject[count];
        SpriteRenderer[] puffSrs = new SpriteRenderer[count];
        Vector2[] velocities = new Vector2[count];
        float[] startScales = new float[count];

        float baseAngle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
        float backAngle = baseAngle + 180f;
        Vector3 spawnPos = transform.position;

        for (int i = 0; i < count; i++)
        {
            GameObject puffGo = new GameObject("SmokePuff" + i);
            puffGo.transform.position = spawnPos;
            SpriteRenderer sr = puffGo.AddComponent<SpriteRenderer>();
            sr.sprite = _circleSprite;
            sr.sortingOrder = 50;
            sr.color = Color.clear;
            puffGos[i] = puffGo;
            puffSrs[i] = sr;

            float spread = Random.Range(-puffSpreadAngle * 0.5f, puffSpreadAngle * 0.5f);
            float angle = (backAngle + spread) * Mathf.Deg2Rad;
            float speed = Random.Range(puffMinSpeed, puffMaxSpeed);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            startScales[i] = Random.Range(puffMinScale, puffMaxScale);
        }

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / puffLifetime, 1f);
            for (int i = 0; i < count; i++)
            {
                if (puffSrs[i] == null) continue;
                puffGos[i].transform.position += (Vector3)(velocities[i] * Time.deltaTime);
                float sc = Mathf.Lerp(startScales[i], startScales[i] * 2f, t);
                puffGos[i].transform.localScale = Vector3.one * sc;
                Color c = puffColor;
                c.a = Mathf.Lerp(puffColor.a, 0f, t);
                puffSrs[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (puffGos[i] != null)
                Destroy(puffGos[i]);
        }
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
