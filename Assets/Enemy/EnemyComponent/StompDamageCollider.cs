using UnityEngine;

public class StompDamageCollider : MonoBehaviour
{
    [SerializeField] private float damageAmount = 1f;

    private bool hasDamagedPlayer;
    private Collider2D hitboxCollider;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }
    }

    public void SetDamage(float damage)
    {
        damageAmount = damage;
    }

    public void ResetForAttack()
    {
        hasDamagedPlayer = false;
    }

    public Collider2D GetHitboxCollider()
    {
        return hitboxCollider;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasDamagedPlayer)
        {
            return;
        }

        Player player = other.GetComponentInParent<Player>();
        if (player == null)
        {
            return;
        }

        player.TakeDamage(damageAmount);
        hasDamagedPlayer = true;
    }
}
