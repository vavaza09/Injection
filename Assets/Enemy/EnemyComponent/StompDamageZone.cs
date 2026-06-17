using UnityEngine;

public class StompDamageZone : MonoBehaviour
{
    [SerializeField] public float damage = 20f;
    [SerializeField] public float knockbackForce = 12f;
    [SerializeField] public float knockbackUpward = 0.5f;

    private Collider2D _col;
    private bool _hasHit;
    private Transform _stomperRoot;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _stomperRoot = transform.parent;
        if (_col != null)
        {
            _col.isTrigger = true;
            _col.enabled = false;
        }
    }

    public void EnableZone()
    {
        _hasHit = false;
        if (_col != null) _col.enabled = true;
    }

    public void DisableZone()
    {
        if (_col != null) _col.enabled = false;
    }

    // Fires when player walks INTO the active zone
    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);

    // Fires when the zone is ENABLED while player is already inside it
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        if (_hasHit || !other.CompareTag("Player")) return;
        _hasHit = true;

        Player player = other.GetComponentInParent<Player>();
        if (player != null)
            player.TakeDamage(damage);

        Rigidbody2D playerRb = other.GetComponentInParent<Rigidbody2D>();
        if (playerRb != null && knockbackForce > 0f)
        {
            Vector3 stomperPos = _stomperRoot != null ? _stomperRoot.position : transform.position;
            float dir = Mathf.Sign(other.transform.position.x - stomperPos.x);
            if (dir == 0f) dir = 1f;

            Vector2 kick = new Vector2(dir, knockbackUpward).normalized;
            playerRb.linearVelocity = Vector2.zero;
            playerRb.AddForce(kick * knockbackForce, ForceMode2D.Impulse);
        }
    }
}
