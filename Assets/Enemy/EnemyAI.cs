using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyState currentState = EnemyState.Idle;
    
    [Header("Patrol Settings")]
    [SerializeField] private float patrolRange = 3f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float idleTimeAtEdge = 2f;
    
    private float stateTimer;
    private Vector2 patrolStartPosition;
    private Vector2 leftEdge;
    private Vector2 rightEdge;
    private Vector2 currentTarget;
    private bool movingRight;
    private bool isAtEdge;
    private bool patrolInitialized;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }
        
        stateTimer = 0f;
        patrolStartPosition = transform.position;
        
        // Calculate patrol edges ONCE at start
        leftEdge = new Vector2(patrolStartPosition.x - patrolRange, patrolStartPosition.y);
        rightEdge = new Vector2(patrolStartPosition.x + patrolRange, patrolStartPosition.y);
        
        // Start moving right
        movingRight = true;
        currentTarget = rightEdge;
        isAtEdge = false;
        patrolInitialized = true;
        
        // Set initial facing direction
        FlipSprite(movingRight);
    }

    private void Update()
    {
        UpdateAI();
    }

    public void UpdateAI()
    {
        if (enemy == null)
        {
            return;
        }

        if (enemy.GetState() == EnemyState.Stunned && currentState != EnemyState.Stunned)
            ChangeState(EnemyState.Stunned);

        switch (currentState)
        {
            case EnemyState.Idle:
                Idle();
                break;
            case EnemyState.Patrol:
                Patrol();
                break;
            case EnemyState.Chase:
                Chase();
                break;
            case EnemyState.Attack:
                Attack();
                break;
            case EnemyState.Stunned:
                break;
            case EnemyState.Dead:
                break;
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (newState == currentState)
        {
            return;
        }

        currentState = newState;
        stateTimer = 0f;

        enemy.SetState(newState);

     
        if (newState == EnemyState.Patrol)
        {
            // Only recalculate if not initialized yet
            if (!patrolInitialized)
            {
                patrolStartPosition = transform.position;
                leftEdge = new Vector2(patrolStartPosition.x - patrolRange, patrolStartPosition.y);
                rightEdge = new Vector2(patrolStartPosition.x + patrolRange, patrolStartPosition.y);
                patrolInitialized = true;   
            }
            
            isAtEdge = false;
        }
        
        // When stopping chase, flip to face back towards patrol area
        if (newState == EnemyState.Patrol)
        {
            // Determine which direction to face based on current target
            bool shouldFaceRight = currentTarget.x > transform.position.x;
            FlipSprite(shouldFaceRight);
        }
    }

    private void Idle()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() != EnemyState.Idle)
        {
            ChangeState(enemy.GetState());
            return;
        }
            
        stateTimer += Time.deltaTime;

        // Check if we're idling at an edge
        if (isAtEdge)
        {
            // Wait at edge for specified time
            if (stateTimer >= idleTimeAtEdge)
            {
                // Turn around and go back to patrol
                if (movingRight)
                {
                    movingRight = false;
                    currentTarget = leftEdge;
                    FlipSprite(false); // Face left
                }
                else
                {
                    movingRight = true;
                    currentTarget = rightEdge;
                    FlipSprite(true); // Face right
                }
                
                isAtEdge = false;
                ChangeState(EnemyState.Patrol);
            }
        }
        else
        {
            // Normal idle at start
            if (stateTimer >= 2f)
            {
                ChangeState(EnemyState.Patrol);
            }
        }
    }

    private void Patrol()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() != EnemyState.Patrol)
        {
            ChangeState(enemy.GetState());
            return;
        }

 
        Vector2 currentPos = transform.position;
        
        // Check if reached edge
        float distanceToTarget = Mathf.Abs(currentPos.x - currentTarget.x);
        
        if (distanceToTarget < 0.2f)
        {
            // Reached edge, switch to idle state
            isAtEdge = true;
            ChangeState(EnemyState.Idle);
        }
        else
        {
            // Move towards current target edge
            float directionX = currentTarget.x - currentPos.x;
            Vector2 direction = new Vector2(directionX, 0f).normalized;
            enemy.Patrol(direction, patrolSpeed);
            
            // Update facing direction while moving
            bool shouldFaceRight = directionX > 0;
            FlipSprite(shouldFaceRight);
        }
    }

    private void Chase()
    {
        enemy.ChasePlayer();
        enemy.DetectPlayer();
     
        if (enemy.transform.position.x < enemy.GetComponent<Enemy>().playerTransform.position.x)
        {
            FlipSprite(true); 
        }
        else
        {
            FlipSprite(false); 
        }
        
        if (enemy.GetState() != EnemyState.Chase)
        {
            ChangeState(enemy.GetState());
        }
    }

    private void Attack()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() != EnemyState.Attack)
        {
            ChangeState(enemy.GetState());
            return;
        }

        stateTimer += Time.deltaTime;
        if (stateTimer >= 0.6f)
        {
            enemy.Attack();
            stateTimer = 0f;
        }
    }


    private void FlipSprite(bool faceRight)
    {
        if (enemy == null)
        {
            return;
        }

        Vector3 scale = enemy.transform.localScale;
        
        if (faceRight)
        {
            // Face right (positive scale)
            if (scale.x < 0)
            {
                scale.x = Mathf.Abs(scale.x);
                enemy.transform.localScale = scale;
            }
        }
        else
        {
            // Face left (negative scale)
            if (scale.x > 0)
            {
                scale.x = -Mathf.Abs(scale.x);
                enemy.transform.localScale = scale;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 startPos;
        Vector2 left;
        Vector2 right;
        
        if (Application.isPlaying)
        {
            startPos = patrolStartPosition;
            left = leftEdge;
            right = rightEdge;
        }
        else
        {
            startPos = transform.position;
            left = new Vector2(startPos.x - patrolRange, startPos.y);
            right = new Vector2(startPos.x + patrolRange, startPos.y);
        }
        
        // Draw patrol range line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(left, right);
        
        // Draw left edge (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(left, 0.3f);
        Gizmos.DrawLine(left + Vector2.up * 0.5f, left + Vector2.down * 0.5f);
        
        // Draw right edge (red)
        Gizmos.DrawWireSphere(right, 0.3f);
        Gizmos.DrawLine(right + Vector2.up * 0.5f, right + Vector2.down * 0.5f);
        
        // Draw patrol center (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPos, 0.2f);
        
        // Draw current target when playing
        if (Application.isPlaying && currentState == EnemyState.Patrol)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentTarget, 0.25f);
            Gizmos.DrawLine(transform.position, currentTarget);
            
            // Draw direction arrow
            Vector3 arrowEnd;
            if (movingRight)
            {
                Gizmos.color = Color.blue;
                arrowEnd = transform.position + Vector3.right * 0.5f;
            }
            else
            {
                Gizmos.color = Color.blue;
                arrowEnd = transform.position + Vector3.left * 0.5f;
            }
            Gizmos.DrawLine(transform.position, arrowEnd);
        }
        
        // Show when at edge in idle
        if (Application.isPlaying && currentState == EnemyState.Idle && isAtEdge)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
