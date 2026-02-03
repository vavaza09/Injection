using Game.Components.Health;
using Game.Components.Movement;
using UnityEngine;

public class Enemy : character
{
    [Header("Enemy Settings")]
    [SerializeField] protected EnemyType enemyType;

    [Header("Detection")]
    [SerializeField] protected float detectionRange = 6f;
    [SerializeField] protected float attackRange = 1.5f;

    [Header("Target")]
    [SerializeField] public Transform playerTransform; // Changed from protected to public

    [Header("State")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;

    protected override void Start()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();

        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: Player not found! Make sure player has 'Player' tag.");
            }
        }

        base.Start();
    }


    public override void Move(Vector2 direction)
    {
        // Prefer MovementComponent (recommended)
        if (movementComponent != null)
        {
            movementComponent.Move(direction);
        }
        else
        {
            transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Patrol movement with custom speed.
    /// </summary>
    public virtual void Patrol(Vector2 direction, float speed)
    {
        if (movementComponent != null)
        {
            // Use custom patrol speed
            Vector2 movement = direction.normalized * speed * Time.deltaTime;
            transform.position += (Vector3)movement;
        }
        else
        {
            transform.position += (Vector3)(direction.normalized * speed * Time.deltaTime);
        }
    }

    public override void Attack()
    {
        //if (attackComponent != null)
        //{
        //    attackComponent.PerformAttack();
        //}
    }

    protected override void OnDeath()
    {
        currentState = EnemyState.Dead;

        if (movementComponent != null)
        {
            movementComponent.Stop();
        }
        // Optional: disable collisions, play anim, etc.
        // GetComponent<Collider2D>()?.enabled = false;
    }

    protected override void OnTakeDamage()
    {
        if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
        {
            currentState = EnemyState.Chase;
        }
    }


    public virtual void DetectPlayer()
    {
        // Early return if player not found
        if (playerTransform == null)
        {
            // Try to find player again (in case it spawned later)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                // Player still not found, stay in current state
                return;
            }
        }

        // Check if player transform is still valid (player might have been destroyed)
        if (playerTransform == null)
        {
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (dist <= detectionRange)
        {
            currentState = EnemyState.Chase;
        }
        else if (currentState != EnemyState.Idle && currentState != EnemyState.Patrol)
        {
            currentState = EnemyState.Patrol;
        }
    }

    public virtual void ChasePlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector2 dir = playerTransform.position - transform.position;
        dir.y = 0f;

        Move(dir.normalized);
    }

    public virtual void PatrolArea()
    {
        // Stop movement (called when waiting at patrol point)
        Move(Vector2.zero);
    }

    public void SetState(EnemyState state)
    {
        currentState = state;
    }

    public EnemyState GetState()
    {
        return currentState;
    }
}
