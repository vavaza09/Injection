using UnityEngine;
using System.Collections;

public class FurnaceProjectile : MonoBehaviour
{
    [Header("Furnace Projectile")]
    public float fallSpeed = 18f;
    public Vector2 hitboxSize = new Vector2(1.5f, 1.5f);
    public float damageAmount = 1f;

    [Header("Ground Shadow")]
    [SerializeField] private float shadowMaxRadius = 1.0f;
    [SerializeField] private float shadowMinRadius = 0.15f;
    [SerializeField] private float shadowMaxAlpha  = 0.45f;
    [SerializeField] private float shadowMinAlpha  = 0.05f;
    [SerializeField] private int   shadowSortOrder  = 1;

    [SerializeField] private float hitboxActiveDuration = 0.2f;

    private LayerMask groundLayerMask;
    private BoxCollider2D damageHitbox;
    private bool hasImpactedGround;
    private bool hasDamagedPlayer;
    private Vector3 impactPosition;

    private GameObject _shadow;
    private SpriteRenderer _shadowSr;
    private float _shadowGroundY;
    private float _initialFallHeight;

    private float _explosionRadius;
    private bool _hasExplosion;

    private void Awake()
    {
        groundLayerMask = LayerMask.GetMask("Ground");
        damageHitbox = GetComponent<BoxCollider2D>();
        if (damageHitbox == null)
            damageHitbox = gameObject.AddComponent<BoxCollider2D>();

        damageHitbox.isTrigger = true;
        damageHitbox.enabled = false;
        damageHitbox.size = hitboxSize;
    }

    private void Start()
    {
        if (GetComponent<TrailRenderer>() == null)
            SetupTrail();

        CreateGroundShadow();
    }
    
    public void Initialize(float configuredFallSpeed, Vector2 configuredHitboxSize, float configuredDamageAmount)
    {
        fallSpeed = configuredFallSpeed;
        hitboxSize = configuredHitboxSize;
        damageAmount = configuredDamageAmount;

        if (damageHitbox != null)
            damageHitbox.size = hitboxSize;
    }

    public void InitializeWithExplosion(float speed, Vector2 hitbox, float damage, float explRadius)
    {
        Initialize(speed, hitbox, damage);
        _explosionRadius = explRadius;
        _hasExplosion = true;
    }

    private void Update()
    {
        if (hasImpactedGround)
            return;

        UpdateShadow();

        float stepDistance = fallSpeed * Time.deltaTime;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, stepDistance + 0.05f, groundLayerMask);
        if (hit.collider != null)
        {
            impactPosition = hit.point;
            transform.position = impactPosition;
            StartCoroutine(ActivateHitboxThenDestroy());
            return;
        }

        transform.position += Vector3.down * stepDistance;
    }

    private void OnDestroy()
    {
        if (_shadow != null)
            Destroy(_shadow);
    }

    private void CreateGroundShadow()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 60f, groundLayerMask);
        if (hit.collider == null)
            return;

        _shadowGroundY = hit.point.y;
        _initialFallHeight = transform.position.y - _shadowGroundY;
        if (_initialFallHeight < 0.01f)
            return;

        _shadow = new GameObject("FurnaceProjectile_Shadow");
        _shadow.transform.position = new Vector3(transform.position.x, _shadowGroundY + 0.02f, transform.position.z);
        _shadow.transform.localScale = Vector3.one * shadowMinRadius;

        _shadowSr = _shadow.AddComponent<SpriteRenderer>();
        _shadowSr.sprite = CreateCircleSprite(32);
        _shadowSr.color = new Color(0f, 0f, 0f, shadowMinAlpha);
        _shadowSr.sortingOrder = shadowSortOrder;
    }

    private void UpdateShadow()
    {
        if (_shadow == null || _initialFallHeight < 0.01f)
            return;

        float remaining = transform.position.y - _shadowGroundY;
        float t = Mathf.Clamp01(1f - remaining / _initialFallHeight);

        float radius = Mathf.Lerp(shadowMinRadius, shadowMaxRadius, t);
        _shadow.transform.localScale = Vector3.one * radius;

        float alpha = Mathf.Lerp(shadowMinAlpha, shadowMaxAlpha, t);
        _shadowSr.color = new Color(0f, 0f, 0f, alpha);
    }

    private IEnumerator ActivateHitboxThenDestroy()
    {
        hasImpactedGround = true;

        if (_hasExplosion)
        {
            Player hitPlayer = null;
            Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)impactPosition, _explosionRadius);
            foreach (var col in hits)
            {
                if (hitPlayer != null) break;
                hitPlayer = col.GetComponentInParent<Player>();
                if (hitPlayer != null)
                    hitPlayer.TakeDamage(damageAmount);
            }
            SpawnExplosionEffect(impactPosition, _explosionRadius);
            Destroy(gameObject);
            yield break;
        }

        if (damageHitbox != null)
            damageHitbox.enabled = true;

        yield return new WaitForSeconds(hitboxActiveDuration);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasImpactedGround || hasDamagedPlayer)
            return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null)
            return;

        player.TakeDamage(damageAmount);
        hasDamagedPlayer = true;
    }

    private static void SpawnExplosionEffect(Vector3 pos, float radius)
    {
        var parent = new GameObject("FurnaceExplosion");
        parent.transform.position = pos;

        // A) Expanding ring
        var ringObj = new GameObject("Ring");
        ringObj.transform.SetParent(parent.transform, false);
        var ringSR = ringObj.AddComponent<SpriteRenderer>();
        ringSR.sprite = CreateRingSprite(64);
        ringSR.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        ringSR.sortingOrder = 6;
        ringObj.transform.localScale = Vector3.zero;

        // B) Flash circle
        var flashObj = new GameObject("Flash");
        flashObj.transform.SetParent(parent.transform, false);
        var flashSR = flashObj.AddComponent<SpriteRenderer>();
        flashSR.sprite = CreateCircleSprite(32);
        flashSR.color = new Color(1f, 0.95f, 0.7f, 1f);
        flashSR.sortingOrder = 7;
        flashObj.transform.localScale = Vector3.one * radius * 0.5f;

        // C) Spark particles
        SetupSparks(parent.transform);

        // D) Smoke particles
        SetupSmoke(parent.transform);

        var effect = parent.AddComponent<FurnaceExplosionEffect>();
        effect.Setup(ringSR, flashSR, radius);
    }

    private static void SetupSparks(Transform parent)
    {
        var obj = new GameObject("Sparks");
        obj.transform.SetParent(parent, false);
        var ps = obj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.gravityModifier = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(20, 30))
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0f), 0.5f),
                new GradientColorKey(new Color(0.4f, 0.05f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        obj.GetComponent<ParticleSystemRenderer>().material =
            new Material(Shader.Find("Sprites/Default"));

        ps.Play();
    }

    private static void SetupSmoke(Transform parent)
    {
        var obj = new GameObject("Smoke");
        obj.transform.SetParent(parent, false);
        var ps = obj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.gravityModifier = -0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.25f, 0.15f, 0.1f, 0.7f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(8, 12))
        });

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.25f, 0.15f, 0.1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.12f, 0.08f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.4f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.5f, 1.2f),
                new Keyframe(1f, 1f)
            ));

        obj.GetComponent<ParticleSystemRenderer>().material =
            new Material(Shader.Find("Sprites/Default"));

        ps.Play();
    }

    private void SetupTrail()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.4f;
        trail.startWidth = 0.2f;
        trail.endWidth = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.4f),
                new GradientColorKey(new Color(0.8f, 0.1f, 0f),  1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0.7f, 0.4f),
                new GradientAlphaKey(0f,  1f)
            }
        );
        trail.colorGradient = gradient;
    }

    private static Sprite CreateRingSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = dist / center;
                float alpha;
                if (t < 0.7f || t > 1.0f)
                    alpha = 0f;
                else if (t < 0.8f)
                    alpha = (t - 0.7f) / 0.1f;
                else if (t < 0.9f)
                    alpha = 1f;
                else
                    alpha = 1f - (t - 0.9f) / 0.1f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 drawCenter = hasImpactedGround ? impactPosition : transform.position;
        Vector3 drawSize = new Vector3(hitboxSize.x, hitboxSize.y, 0.05f);
        Gizmos.DrawWireCube(drawCenter, drawSize);
    }
}
