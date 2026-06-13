using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class HammerHitbox : MonoBehaviour
{
    [SerializeField] private int damage = 20;
    [SerializeField] private float lifetime = 0.4f;

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 0.3f);

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        character player = other.GetComponent<character>();
        player?.TakeDamage(damage);
        Destroy(gameObject);
    }
}
