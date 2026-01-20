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

    [Header("Visual Effects")]
    [SerializeField] private GameObject attackEffectPrefab;
    [SerializeField] private Transform attackPoint;

    // Attack state
    private enum AttackPhase { None, Anticipation, Strike, Recovery }
    private AttackPhase currentPhase = AttackPhase.None;

    private float lastAttackTime = -999f;
    private float lastJumpAttackTime = -999f;
    private float phaseTimer = 0f;
    private bool isAttacking = false;

    // Components
    private MeleeEnemyMovement movementComponent;

    // Public properties
    public bool IsAttacking => isAttacking;
    public bool CanAttack => !isAttacking && Time.time >= lastAttackTime + attackCooldown;

    private void Awake()
    {
        movementComponent = GetComponent<MeleeEnemyMovement>();
    }

    /// <summary>
    /// Attempt to perform a regular melee attack
    /// </summary>
    public void TryAttack(Transform target)
    {
        if (target == null || !CanAttack) return;

        float distance = Vector2.Distance(transform.position, target.position);

        // Check if in range
        if (distance <= meleeRange)
        {
            StartAttack();
        }
    }

    /// <summary>
    /// Attempt to perform a jump attack if target is above
    /// </summary>
    public void TryJumpAttack(Transform target)
    {
        if (!enableJumpAttack || target == null) return;
        if (isAttacking || Time.time < lastJumpAttackTime + jumpAttackCooldown) return;

        float distance = Vector2.Distance(transform.position, target.position);
        float heightDifference = target.position.y - transform.position.y;

        // Check if target is above and in range
        if (heightDifference >= jumpAttackMinHeight && distance <= meleeRange * 2f)
        {
            PerformJumpAttack(target.position);
        }
    }

    /// <summary>
    /// Update the attack state machine (call this in Update or from EnemyAI)
    /// </summary>
    public void UpdateAttack()
    {
        if (!isAttacking) return;

        phaseTimer += Time.deltaTime;

        switch (currentPhase)
        {
            case AttackPhase.Anticipation:
                UpdateAnticipationPhase();
                break;

            case AttackPhase.Strike:
                UpdateStrikePhase();
                break;

            case AttackPhase.Recovery:
                UpdateRecoveryPhase();
                break;
        }
    }

    /// <summary>
    /// Check if target is in attack range
    /// </summary>
    public bool IsTargetInRange(Transform target)
    {
        if (target == null) return false;
        return Vector2.Distance(transform.position, target.position) <= meleeRange;
    }

    /// <summary>
    /// Force cancel current attack
    /// </summary>
    public void CancelAttack()
    {
        isAttacking = false;
        currentPhase = AttackPhase.None;
        phaseTimer = 0f;
    }

    private void StartAttack()
    {
        isAttacking = true;
        currentPhase = AttackPhase.Anticipation;
        phaseTimer = 0f;

        // Stop movement during attack
        if (movementComponent != null)
        {
            movementComponent.StopMovement();
        }

        // Optional:  Trigger animation
        // animator?.SetTrigger("AttackAnticipation");
    }

    private void UpdateAnticipationPhase()
    {
        // Wind-up phase - enemy telegraphs attack
        if (phaseTimer >= anticipationTime)
        {
            currentPhase = AttackPhase.Strike;
            phaseTimer = 0f;
            ExecuteStrike();
        }
    }

    private void UpdateStrikePhase()
    {
        // Active strike phase
        if (phaseTimer >= strikeTime)
        {
            currentPhase = AttackPhase.Recovery;
            phaseTimer = 0f;
        }
    }

    private void UpdateRecoveryPhase()
    {
        // Recovery phase - enemy returns to neutral
        if (phaseTimer >= recoveryTime)
        {
            EndAttack();
        }
    }

    private void ExecuteStrike()
    {
        // Optional: Play attack animation
        // animator?.SetTrigger("Attack");

        // Determine attack position
        Vector2 attackPosition = attackPoint != null
            ? attackPoint.position
            : (Vector2)transform.position + new Vector2(movementComponent.Facing * meleeRange * 0.5f, 0);

        // Create attack hitbox
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPosition, meleeRange * 0.6f, targetLayer);

        foreach (Collider2D hit in hits)
        {
            // Deal damage
            HealthComponent targetHealth = hit.GetComponent<HealthComponent>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(meleeDamage);
            }

            // Apply knockback
            if (applyKnockback)
            {
                ApplyKnockbackToTarget(hit);
            }
        }

        // Spawn visual effect
        if (attackEffectPrefab != null)
        {
            GameObject effect = Instantiate(attackEffectPrefab, attackPosition, Quaternion.identity);
            Destroy(effect, 1f);
        }
    }

    private void ApplyKnockbackToTarget(Collider2D target)
    {
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null && movementComponent != null)
        {
            Vector2 knockbackDirection = new Vector2(movementComponent.Facing, 0.5f).normalized;
            targetRb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
        }
    }

    private void PerformJumpAttack(Vector3 targetPosition)
    {
        if (movementComponent == null) return;

        // Calculate jump direction
        Vector2 direction = (targetPosition - transform.position).normalized;
        direction.y = 0.7f; // Add upward component
        direction.Normalize();

        // Apply jump force
        movementComponent.SetVelocity(Vector2.zero);
        movementComponent.ApplyForce(direction * jumpAttackForce, ForceMode2D.Impulse);

        lastJumpAttackTime = Time.time;

        // Optional: Play jump attack animation
        // animator?.SetTrigger("JumpAttack");
    }

    private void EndAttack()
    {
        isAttacking = false;
        currentPhase = AttackPhase.None;
        lastAttackTime = Time.time;
        phaseTimer = 0f;

        // Optional: Reset animation
        // animator?.SetTrigger("AttackEnd");
    }

    // Visual debugging
    private void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.red;
        Vector3 attackPos = attackPoint != null
            ? attackPoint.position
            : transform.position + new Vector3((movementComponent?.Facing ?? 1) * meleeRange * 0.5f, 0, 0);
        Gizmos.DrawWireSphere(attackPos, meleeRange * 0.6f);

        // Draw melee range circle
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}