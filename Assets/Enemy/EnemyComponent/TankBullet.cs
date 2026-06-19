using UnityEngine;
using System.Collections;

public class TankBullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 4f;

    [Header("Impact VFX")]
    [SerializeField] private float impactFlashScale = 1.5f;
    [SerializeField] private float impactFireballScale = 1.2f;
    [SerializeField] private int impactSparkCount = 20;
    [SerializeField] private float impactSparkSpeed = 10f;
    [SerializeField] private float impactSparkGravity = 2.5f;
    [SerializeField] private int impactSmokeCount = 8;
    [SerializeField] private float impactRingScale = 2f;

    private Vector2 direction = Vector2.right;
    private float damage = 1f;
    private GameObject owner;
    private Vector3 _originalScale;

    private TrailRenderer _trail;
    private ParticleSystem _sparkPS;

    private void Start()
    {
        _originalScale = transform.localScale;
        _trail = GetComponentInChildren<TrailRenderer>();
        _sparkPS = GetComponentInChildren<ParticleSystem>();
        StartCoroutine(BirthFlash());
    }

    private IEnumerator BirthFlash()
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / 0.1f, 1f);
            transform.localScale = _originalScale * Mathf.Lerp(1.5f, 1f, t);
            yield return null;
        }
        transform.localScale = _originalScale;
    }

    public void Initialize(Vector2 shootDirection, float bulletDamage, float bulletSpeed, GameObject bulletOwner)
    {
        direction = shootDirection.sqrMagnitude > 0.0001f ? shootDirection.normalized : Vector2.right;
        damage = bulletDamage;
        speed = bulletSpeed;
        owner = bulletOwner;

        // Orient spark emitter immediately so first frame is correct
        if (_sparkPS != null)
        {
            float angle = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
            _sparkPS.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        // Cone emits along local +Y, subtract 90 to convert from X-axis angle to Y-axis
        if (_sparkPS != null)
        {
            float angle = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
            _sparkPS.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (other == null)
            return;

        if (owner != null)
        {
            Transform ownerRoot = owner.transform.root;
            if (other.gameObject == owner || other.transform.root == ownerRoot)
                return;
        }

        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            player.TakeDamage(damage);
            SpawnImpactVFX();
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            SpawnImpactVFX();
            Destroy(gameObject);
        }
    }

    private void SpawnImpactVFX()
    {
        if (_trail != null)
        {
            _trail.transform.SetParent(null);
            Destroy(_trail.gameObject, _trail.time + 0.1f);
        }

        if (_sparkPS != null)
        {
            _sparkPS.transform.SetParent(null);
            _sparkPS.Stop();
            Destroy(_sparkPS.gameObject, 0.5f);
        }

        GameObject impactGO = new GameObject("BulletImpact");
        impactGO.transform.position = transform.position;
        var vfx = impactGO.AddComponent<TankBulletImpactVFX>();
        vfx.flashMaxScale = impactFlashScale;
        vfx.fireballMaxScale = impactFireballScale;
        vfx.sparkCount = impactSparkCount;
        vfx.sparkMaxSpeed = impactSparkSpeed;
        vfx.sparkGravity = impactSparkGravity;
        vfx.smokeCount = impactSmokeCount;
        vfx.ringMaxScale = impactRingScale;
        vfx.Initialize(direction);
    }
}
