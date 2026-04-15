using UnityEngine;
using System.Collections;

public class FurnaceProjectile : MonoBehaviour
{
    [Header("Furnace Projectile")]
    public float fallSpeed = 18f;
    public Vector2 hitboxSize = new Vector2(1.5f, 1.5f);
    public float damageAmount = 1f;

    [SerializeField] private float hitboxActiveDuration = 0.2f;

    private LayerMask groundLayerMask;
    private BoxCollider2D damageHitbox;
    private bool hasImpactedGround;
    private bool hasDamagedPlayer;
    private Vector3 impactPosition;

    private void Awake()
    {
        groundLayerMask = LayerMask.GetMask("Ground");
        damageHitbox = GetComponent<Collider2D>() as BoxCollider2D;

        if (damageHitbox != null)
        {
            damageHitbox.isTrigger = true;
            damageHitbox.enabled = false;
            damageHitbox.size = hitboxSize;
        }
    }

    public void Initialize(float configuredFallSpeed, Vector2 configuredHitboxSize, float configuredDamageAmount)
    {
        fallSpeed = configuredFallSpeed;
        hitboxSize = configuredHitboxSize;
        damageAmount = configuredDamageAmount;

        if (damageHitbox != null)
        {
            damageHitbox.size = hitboxSize;
        }
    }

    private void Update()
    {
        if (hasImpactedGround)
        {
            return;
        }

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

    private IEnumerator ActivateHitboxThenDestroy()
    {
        hasImpactedGround = true;

        if (damageHitbox != null)
        {
            damageHitbox.enabled = true;
        }

        yield return new WaitForSeconds(hitboxActiveDuration);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasImpactedGround)
        {
            return;
        }

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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector3 drawCenter = hasImpactedGround ? impactPosition : transform.position;
        Vector3 drawSize = new Vector3(hitboxSize.x, hitboxSize.y, 0.05f);
        Gizmos.DrawWireCube(drawCenter, drawSize);
    }
}
