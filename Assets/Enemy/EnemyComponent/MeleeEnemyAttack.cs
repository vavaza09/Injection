using UnityEngine;
using Game.Components.Health;

public class MeleeEnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float meleeDamage = 10f;
    [SerializeField] private float meleeRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private LayerMask targetLayer;

    [Header("Attack Phases")]
    [SerializeField] private float anticipationTime = 0.3f;
    [SerializeField] private float strikeTime = 0.2f;
    [SerializeField] private float recoveryTime = 0.3f;

    [Header("Knockback")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private float knockbackForce = 8f;

    [Header("Jump Attack (Optional)")]
    [SerializeField] private bool enableJumpAttack = false;
    [SerializeField] private float jumpAttackForce = 10f;
    [SerializeField] private float jumpAttackCooldown = 3f;
    [SerializeField] private float jumpAttackMinHeight = 1f;

    private enum AttackPhase { None, Anticipation, Strike, Recovery }
    private AttackPhase currentPhase = AttackPhase.None;

    private float lastAttackTime = -999f;
    private float lastJumpAttackTime = -999f;
    private float phaseTimer = 0f;
    private bool isAttacking = false;

    private MeleeEnemyMovement movement;
    private Rigidbody2D rb;

    public bool IsAttacking => isAttacking;
    public bool CanAttack => !isAttacking && Time.time >= lastAttackTime + attackCooldown;

    private void Awake()
    {
        movement = GetComponent<MeleeEnemyMovement>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void TryAttack(Transform target)
    {
        if (target == null || !CanAttack) return;

        float distance = Vector2.Distance(transform.position, target.position);

        if (distance <= meleeRange)
        {
            StartAttack();
        }
    }

    public void TryJumpAttack(Transform target)
    {
        if (!enableJumpAttack || target == null) return;
        if (isAttacking || Time.time < lastJumpAttackTime + jumpAttackCooldown) return;

        float distance = Vector2.Distance(transform.position, target.position);
        float heightDifference = target.position.y - transform.position.y;

        if (heightDifference >= jumpAttackMinHeight && distance <= meleeRange * 2f)
        {
            PerformJumpAttack(target.position);
        }
    }

    public void UpdateAttack()
    {
        if (!isAttacking) return;

        phaseTimer += Time.deltaTime;

        switch (currentPhase)
        {
            case AttackPhase.Anticipation:
                if (phaseTimer >= anticipationTime)
                {
                    currentPhase = AttackPhase.Strike;
                    phaseTimer = 0f;
                    ExecuteStrike();
                }
                break;

            case AttackPhase.Strike:
                if (phaseTimer >= strikeTime)
                {
                    currentPhase = AttackPhase.Recovery;
                    phaseTimer = 0f;
                }
                break;

            case AttackPhase.Recovery:
                if (phaseTimer >= recoveryTime)
                {
                    EndAttack();
                }
                break;
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        currentPhase = AttackPhase.Anticipation;
        phaseTimer = 0f;

        if (movement != null)
        {
            movement.StopMovement();
        }
    }

    private void ExecuteStrike()
    {
        int facing = movement != null ? movement.Facing : 1;
        Vector2 attackPosition = (Vector2)transform.position + new Vector2(facing * meleeRange * 0.5f, 0);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPosition, meleeRange * 0.6f, targetLayer);

        foreach (Collider2D hit in hits)
        {
            HealthComponent targetHealth = hit.GetComponent<HealthComponent>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(meleeDamage);
            }

            if (applyKnockback)
            {
                Rigidbody2D targetRb = hit.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 knockbackDir = new Vector2(facing, 0.5f).normalized;
                    targetRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }
    }

    private void PerformJumpAttack(Vector3 targetPosition)
    {
        if (rb == null) return;

        Vector2 direction = (targetPosition - transform.position).normalized;
        direction.y = 0.7f;
        direction = direction.normalized;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * jumpAttackForce, ForceMode2D.Impulse);

        lastJumpAttackTime = Time.time;
    }

    private void EndAttack()
    {
        isAttacking = false;
        currentPhase = AttackPhase.None;
        lastAttackTime = Time.time;
        phaseTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        int facing = movement != null ? movement.Facing : 1;

        Gizmos.color = Color.red;
        Vector3 attackPos = transform.position + new Vector3(facing * meleeRange * 0.5f, 0, 0);
        Gizmos.DrawWireSphere(attackPos, meleeRange * 0.6f);

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}