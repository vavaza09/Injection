using UnityEngine;
using System.Collections;

public class StompEnemy : Enemy
{
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    [Header("Stomp Attack")]
    [SerializeField] private StompDamageCollider stompDamageCollider;
    [SerializeField] private float preJumpDelay = 0.7f;
    [SerializeField] private float jumpHeightAbovePlayer = 3f;
    [SerializeField] private float jumpToPlayerTime = 0.35f;
    [SerializeField] private float stompDownSpeed = 20f;
    [SerializeField] private float stompDamageDuration = 0.2f;
    [SerializeField] private Vector2 stompHitboxSize = new Vector2(1.5f, 0.8f);
    [SerializeField] private float stompAttackCooldown = 1.5f;
    [SerializeField] private LayerMask groundLayer;

    private bool isAttacking;
    private bool isDropping;
    private bool hasGroundImpact;
    private float lastAttackTime = -999f;
    private BoxCollider2D stompBoxCollider;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }

        if (stompDamageCollider == null)
        {
            stompDamageCollider = GetComponentInChildren<StompDamageCollider>();
        }

        if (stompDamageCollider != null)
        {
            stompDamageCollider.SetDamage(attackDamage);
            stompBoxCollider = stompDamageCollider.GetComponent<Collider2D>() as BoxCollider2D;
            if (stompBoxCollider != null)
            {
                stompBoxCollider.isTrigger = true;
                stompBoxCollider.size = stompHitboxSize;
                stompBoxCollider.enabled = false;
            }
        }
    }

    public override void Move(Vector2 direction)
    {
        if (isAttacking)
        {
            if (movementComponent != null)
            {
                movementComponent.Stop();
            }
            return;
        }

        if (spriteRenderer != null && direction.x != 0f)
        {
            spriteRenderer.flipX = direction.x < 0f;
        }

        base.Move(direction);
    }

    public override void ChasePlayer()
    {
        if (isAttacking)
        {
            Move(Vector2.zero);
            return;
        }

        base.ChasePlayer();
    }

    public override void Attack()
    {
        if (playerTransform == null)
        {
            return;
        }

        if (isAttacking)
        {
            return;
        }

        if (Time.time < lastAttackTime + stompAttackCooldown)
        {
            return;
        }

        StartCoroutine(StompAttackRoutine());
    }

    private IEnumerator StompAttackRoutine()
    {
        isAttacking = true;
        isDropping = false;
        hasGroundImpact = false;
        lastAttackTime = Time.time;

        if (anim != null)
        {
            anim.Play("Attack");
        }

        if (spriteRenderer != null && playerTransform != null)
        {
            spriteRenderer.flipX = playerTransform.position.x < transform.position.x;
        }

        yield return new WaitForSeconds(preJumpDelay);

        if (movementComponent != null)
        {
            movementComponent.SetCanMove(false);
            movementComponent.Stop();
        }

        Vector3 startPosition = transform.position;
        Vector3 targetAbovePlayer = new Vector3(
            playerTransform.position.x,
            playerTransform.position.y + jumpHeightAbovePlayer,
            transform.position.z);

        float timer = 0f;
        while (timer < jumpToPlayerTime)
        {
            timer += Time.deltaTime;
            float t = jumpToPlayerTime > 0f ? Mathf.Clamp01(timer / jumpToPlayerTime) : 1f;
            transform.position = Vector3.Lerp(startPosition, targetAbovePlayer, t);
            yield return null;
        }

        isDropping = true;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, -Mathf.Abs(stompDownSpeed));
        }

        while (!hasGroundImpact)
        {
            if (HasLanded())
            {
                hasGroundImpact = true;
                break;
            }

            yield return null;
        }

        isDropping = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        yield return StartCoroutine(EnableStompDamageWindow());

        if (movementComponent != null)
        {
            movementComponent.SetCanMove(true);
        }

        if (anim != null)
        {
            anim.Play("Walk");
        }

        isAttacking = false;
    }

    private bool HasLanded()
    {
        Vector2 origin = transform.position;
        if (stompBoxCollider != null)
        {
            origin = stompBoxCollider.bounds.center;
            origin.y = stompBoxCollider.bounds.min.y + 0.02f;
        }

        const float checkDistance = 0.2f;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, groundLayer);
        return hit.collider != null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TryMarkGroundImpact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryMarkGroundImpact(other);
    }

    private void TryMarkGroundImpact(Collider2D other)
    {
        if (!isDropping)
        {
            return;
        }

        if (other == null)
        {
            return;
        }

        if (!IsGroundLayer(other.gameObject.layer))
        {
            return;
        }

        hasGroundImpact = true;
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundLayer.value & (1 << layer)) != 0;
    }

    private IEnumerator EnableStompDamageWindow()
    {
        if (stompDamageCollider == null)
        {
            yield break;
        }

        stompDamageCollider.SetDamage(attackDamage);
        stompDamageCollider.ResetForAttack();

        Collider2D hitbox = stompDamageCollider.GetHitboxCollider();
        if (hitbox == null)
        {
            yield break;
        }

        hitbox.enabled = true;
        yield return new WaitForSeconds(stompDamageDuration);
        hitbox.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(stompHitboxSize.x, stompHitboxSize.y, 0.05f);

        if (stompDamageCollider != null)
        {
            BoxCollider2D box = stompDamageCollider.GetComponent<Collider2D>() as BoxCollider2D;
            if (box != null)
            {
                center = box.bounds.center;
                size = new Vector3(box.size.x, box.size.y, 0.05f);
            }
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, size);

        if (isDropping)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center, center + Vector3.down * 0.2f);
        }
    }
}
