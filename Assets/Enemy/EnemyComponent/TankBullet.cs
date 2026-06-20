using UnityEngine;
using System.Collections;

public class TankBullet : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 4f;
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float knockbackUpward = 0.2f;

    private Vector2 direction = Vector2.right;
    private float damage = 1f;
    private GameObject owner;

    private void Start()
    {
        if (GetComponent<TrailRenderer>() == null)
            SetupTrail();
        StartCoroutine(BirthFlash());
    }

    private void SetupTrail()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = 0.15f;
        trail.endWidth = 0.02f;
        trail.material = new Material(Shader.Find("Sprites/Default"));

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0f),  1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f,  1f)
            }
        );
        trail.colorGradient = gradient;
    }

    private IEnumerator BirthFlash()
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / 0.1f, 1f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, t);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    public void Initialize(Vector2 shootDirection, float bulletDamage, float bulletSpeed, GameObject bulletOwner)
    {
        direction = shootDirection.sqrMagnitude > 0.0001f ? shootDirection.normalized : Vector2.right;
        damage = bulletDamage;
        speed = bulletSpeed;
        owner = bulletOwner;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
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
        {
            return;
        }

        if (owner != null)
        {
            Transform ownerRoot = owner.transform.root;
            if (other.gameObject == owner || other.transform.root == ownerRoot)
            {
                return;
            }
        }

        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            if (player.TakeDamage(damage) && knockbackForce > 0f)
                player.ApplyKnockback(transform.position, knockbackForce, knockbackUpward);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
