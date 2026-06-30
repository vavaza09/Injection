using UnityEngine;
using System.Collections;

// One falling junk piece spawned by BossJunkSmashAttack.
// Raycasts down to find landing surface (Platform or Ground), casts a growing shadow telegraph,
// plays stomp impact VFX on landing, and area-damages the player.
// Uses a procedural white-square sprite as a placeholder — swap in real art later.
[RequireComponent(typeof(SpriteRenderer))]
public class BossJunk : MonoBehaviour
{
    [SerializeField] private float fallSpeed             = 14f;
    [SerializeField] private float junkScale             = 0.2f;
    [SerializeField] private float fallingColliderRadius = 0.4f;
    [SerializeField] private float shadowMinRadius = 0.15f;
    [SerializeField] private float shadowMaxRadius = 1.0f;
    [SerializeField] private float shadowMinAlpha  = 0.05f;
    [SerializeField] private float shadowMaxAlpha  = 0.45f;
    [SerializeField] private int   shadowSortOrder = 1;

    private LayerMask     _groundMask;
    private StompImpactVFX _vfxPrefab;
    private float         _vfxRadius;
    private float         _damage;

    [SerializeField] private float spinSpeedMin = 90f;
    [SerializeField] private float spinSpeedMax = 270f;

    [Header("Audio")]
    [SerializeField] private SfxCue fallSfx;
    [SerializeField] private SfxCue impactSfx;
    [SerializeField] private float shakeIntensity = 0.25f;
    [SerializeField] private float shakeDuration  = 0.2f;

    private float         _landingY;
    private float         _initialHeight;
    private bool          _initialized;
    private bool          _landed;
    private bool          _hitPlayerDuringFall;
    private float         _spinSpeed;
    private CircleCollider2D _fallingCollider;

    private AudioSource    _sfxSource;
    private GameObject    _shadow;
    private SpriteRenderer _shadowSr;

    // ── Public init called by BossJunkSmashAttack ─────────────────────────

    public void Initialize(LayerMask groundMask, StompImpactVFX vfxPrefab, float vfxRadius, float damage)
    {
        _groundMask = groundMask;
        _vfxPrefab  = vfxPrefab;
        _vfxRadius  = vfxRadius;
        _damage     = damage;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        BossSfx.Make3D(_sfxSource);

        SetupVisual();
        SetupShadow();
        SetupFallingCollider();
        _spinSpeed   = Random.Range(spinSpeedMin, spinSpeedMax) * (Random.value < 0.5f ? 1f : -1f);
        _initialized = true;

        BossSfx.Play(this, SoundType.BOSS_JUNK_FALL, fallSfx, _sfxSource);
    }

    // ── Update ────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_initialized || _landed) return;

        UpdateShadow();

        transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);

        float step = fallSpeed * Time.deltaTime;

        // Check if we'll reach the landing Y this frame.
        if (transform.position.y - step <= _landingY)
        {
            transform.position = new Vector3(transform.position.x, _landingY, transform.position.z);
            Land();
            return;
        }

        transform.position += Vector3.down * step;
    }

    private void OnDestroy()
    {
        if (_shadow != null) Destroy(_shadow);
    }

    // ── Falling damage ────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_landed || _hitPlayerDuringFall || !_initialized) return;
        var player = other.GetComponentInParent<Player>();
        if (player == null) return;
        player.TakeDamage(_damage);
        _hitPlayerDuringFall = true;
    }

    // ── Landing ───────────────────────────────────────────────────────────

    private void Land()
    {
        _landed = true;
        if (_fallingCollider != null) _fallingCollider.enabled = false;
        if (_shadow != null) Destroy(_shadow);

        BossSfx.Play(this, SoundType.BOSS_JUNK_IMPACT, impactSfx, _sfxSource);
        CameraShake.Shake(shakeIntensity, shakeDuration);

        if (_vfxPrefab != null)
        {
            var vfx = Instantiate(_vfxPrefab, transform.position, Quaternion.identity);
            vfx.Initialize(_vfxRadius);
        }
        else
        {
            var go = new GameObject("JunkImpactVFX");
            go.transform.position = transform.position;
            go.AddComponent<StompImpactVFX>().Initialize(_vfxRadius);
        }

        // Area-damage — one hit per player; use GetComponentInParent (player is on Cape layer 10).
        bool hit = false;
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, _vfxRadius))
        {
            if (hit) break;
            var player = col.GetComponentInParent<Player>();
            if (player != null)
            {
                player.TakeDamage(_damage);
                hit = true;
            }
        }

        StartCoroutine(LingerThenDestroy());
    }

    private IEnumerator LingerThenDestroy()
    {
        // Fade out the junk sprite over 0.3s.
        var sr = GetComponent<SpriteRenderer>();
        float t = 0f;
        Color c = sr != null ? sr.color : Color.white;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            if (sr != null) { c.a = Mathf.Lerp(1f, 0f, t / 0.3f); sr.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── Falling collider ──────────────────────────────────────────────────

    private void SetupFallingCollider()
    {
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic  = true;
        rb.gravityScale = 0f;
        _fallingCollider = gameObject.AddComponent<CircleCollider2D>();
        _fallingCollider.isTrigger = true;
        _fallingCollider.radius    = fallingColliderRadius;
    }

    // ── Visual setup ──────────────────────────────────────────────────────

    private void SetupVisual()
    {
        var sr = GetComponent<SpriteRenderer>();
        // Only apply the placeholder when no sprite was assigned in the prefab.
        if (sr.sprite == null) sr.sprite = CreateSquareSprite();
        sr.color        = new Color(0.85f, 0.8f, 0.75f, 1f);
        sr.sortingOrder = 5;
        transform.localScale = Vector3.one * junkScale;
    }

    private void SetupShadow()
    {
        // Raycast down to find the landing surface.
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 60f, _groundMask);
        if (hit.collider == null) { _landingY = transform.position.y - 30f; return; }

        _landingY      = hit.point.y;
        _initialHeight = transform.position.y - _landingY;
        if (_initialHeight < 0.01f) return;

        _shadow = new GameObject("BossJunk_Shadow");
        _shadow.transform.position   = new Vector3(transform.position.x, _landingY + 0.02f, 0f);
        _shadow.transform.localScale = Vector3.one * shadowMinRadius;

        _shadowSr               = _shadow.AddComponent<SpriteRenderer>();
        _shadowSr.sprite        = CreateCircleSprite(32);
        _shadowSr.color         = new Color(0f, 0f, 0f, shadowMinAlpha);
        _shadowSr.sortingOrder  = shadowSortOrder;
    }

    private void UpdateShadow()
    {
        if (_shadow == null || _initialHeight < 0.01f) return;

        float remaining = transform.position.y - _landingY;
        float t = Mathf.Clamp01(1f - remaining / _initialHeight);

        _shadow.transform.localScale = Vector3.one * Mathf.Lerp(shadowMinRadius, shadowMaxRadius, t);
        _shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(shadowMinAlpha, shadowMaxAlpha, t));
    }

    // ── Sprite generation ─────────────────────────────────────────────────

    private static Sprite CreateSquareSprite()
    {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color[64];
        for (int i = 0; i < 64; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
    }

    private static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - d / c);
                a *= a;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
