using Game.Components.Health;
using Game.Components.Movement;
using UnityEngine;

public class Enemy : character
{
    [Header("Enemy Settings")]
    [SerializeField] protected EnemyType enemyType = EnemyType.Melee;

    [Header("Detection")]
    [SerializeField] protected float detectionRange = 6f;
    [SerializeField] protected float attackRange = 1.5f;

    [Header("Target")]
    [SerializeField] protected Transform playerTransform;

    [Header("State")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;

    protected override void Start()
    {
        // Cache common references from base fields
        transform = base.transform;
        rigidbody2D = GetComponent<Rigidbody2D>();

        // Grab components if they exist on this object
        healthComponent = GetComponent<HealthComponent>();
        movementComponent = GetComponent<MovementComponent>();
        attackComponent = GetComponent<AttackComponent>();

        // Auto find player by tag if not assigned
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        base.Start();
    }

    private void Update()
    {
        if (!isAlive)
        {
            currentState = EnemyState.Dead;
            return;
        }

        DetectPlayer();
        RunState();
    }

    // =========================
    // Required overrides
    // =========================

    public override void Move(Vector2 direction)
    {
        // Prefer MovementComponent (recommended)
        if (movementComponent != null)
        {
            movementComponent.Move(direction);
            return;
        }

        // Fallback if MovementComponent isn't used
        transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
    }

    public override void Attack()
    {
        // Base enemy attack just uses AttackComponent if present.
        // Derived enemies (MeleeEnemy/RangedEnemy/BossEnemy) can override this.
        //if (attackComponent != null)
        //{
        //    attackComponent.PerformAttack();
        //}
    }

    protected override void OnDeath()
    {
        currentState = EnemyState.Dead;

        // Stop moving when dead (if movement component exists)
        if (movementComponent != null)
            movementComponent.Stop();

        // Optional: disable collisions, play anim, etc.
        // GetComponent<Collider2D>()?.enabled = false;
    }

    protected override void OnTakeDamage()
    {
        // Typical behavior: if hit while idle/patrol, become aggressive
        if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
            currentState = EnemyState.Chase;
    }

    // =========================
    // Enemy logic (diagram)
    // =========================

    public virtual void DetectPlayer()
    {
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (dist <= detectionRange)
        {
            currentState = EnemyState.Chase;
        }
        else
        {
            // If we lost the player, go back to patrol
            if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
                currentState = EnemyState.Patrol;
        }
    }

    public virtual void ChasePlayer()
    {
        if (playerTransform == null) return;

        // Ground-style chase: only move on X (Hollow Knight vibe)
        Vector2 dir = playerTransform.position - transform.position;
        dir.y = 0f;

        Move(dir.normalized);
    }

    public virtual void PatrolArea()
    {
        // Base patrol does nothing (you can override or drive via EnemyAI later)
        Move(Vector2.zero);
    }

    // =========================
    // Simple state machine
    // =========================

    protected virtual void RunState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                // You can add idle timer later; for now go patrol
                currentState = EnemyState.Patrol;
                break;

            case EnemyState.Patrol:
                PatrolArea();
                break;

            case EnemyState.Chase:
                ChasePlayer();
                break;

            case EnemyState.Attack:
                // Only attack if still in range; otherwise chase
                if (playerTransform != null)
                {
                    float dist = Vector2.Distance(transform.position, playerTransform.position);
                    if (dist > attackRange)
                        currentState = EnemyState.Chase;
                    else
                        Attack();
                }
                break;

            case EnemyState.Dead:
                // do nothing
                break;
        }
    }

    // Helpers (optional)
    public EnemyState GetState() => currentState;
    public EnemyType GetEnemyType() => enemyType;
    public void SetPlayerTransform(Transform t) => playerTransform = t;
}
