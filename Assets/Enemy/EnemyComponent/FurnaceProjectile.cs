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
