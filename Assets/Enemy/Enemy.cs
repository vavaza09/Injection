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
    [SerializeField] protected Transform playerTransform;

    [Header("State")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;

    protected override void Start()
    {
        transform = base.transform;
        rigidbody2D = GetComponent<Rigidbody2D>();


        healthComponent = GetComponent<HealthComponent>();
        movementComponent = GetComponent<MovementComponent>();
        attackComponent = GetComponent<AttackComponent>();

        //if (playerTransform == null)
        //{
        //    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        //    if (playerObj != null) playerTransform = playerObj.transform;
        //}

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
            currentState = EnemyState.Patrol;
        }
    }

    public virtual void ChasePlayer()
    {
        if (playerTransform == null) return;

 
        Vector2 dir = playerTransform.position - transform.position;
        dir.y = 0f;

        Move(dir.normalized);
    }

    public virtual void PatrolArea()
    {

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
