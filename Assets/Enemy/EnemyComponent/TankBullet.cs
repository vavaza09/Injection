using UnityEngine;

public class TankBullet : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 4f;

    private Vector2 direction = Vector2.right;
    private float damage = 1f;
    private GameObject owner;

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
        if (owner != null && other.gameObject == owner)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            character target = other.GetComponentInParent<character>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }

            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
