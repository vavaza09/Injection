using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    [Header("Patrol Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float walkDistance = 4f;
    [SerializeField] private float noPlayerWalkDelay = 3f;
    [SerializeField] private float boundaryPauseTime = 0.8f;

    [Header("Ledge / Cliff Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckLookahead = 0.3f;
    [SerializeField] private float edgeCheckDepth = 1.0f;

    private Vector2 patrolCenter;
    private float leftBoundary;
    private float rightBoundary;

    private float stateTimer;
    private float noPlayerTimer;
    private bool movingRight = true;
    private bool isAtBoundary;
    private Collider2D _bodyCollider;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        patrolCenter = transform.position;
        leftBoundary  = patrolCenter.x - walkDistance;
        rightBoundary = patrolCenter.x + walkDistance;

        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground", "Platform");

        _bodyCollider = enemy != null ? enemy.GetComponent<Collider2D>() : GetComponent<Collider2D>();

        FlipSprite(movingRight);
    }

    private void Update()
    {
        UpdateAI();
    }

    public void UpdateAI()
    {
        if (enemy == null) return;

        EnemyState enemyState = enemy.GetState();

        if (enemyState == EnemyState.Stunned && currentState != EnemyState.Stunned)
            ChangeState(EnemyState.Stunned);
        else if (enemyState != EnemyState.Stunned && currentState == EnemyState.Stunned)
            ChangeState(EnemyState.Idle);

        switch (currentState)
        {
            case EnemyState.Idle:    Idle();    break;
            case EnemyState.Patrol:  Patrol();  break;
            case EnemyState.Chase:   Chase();   break;
            case EnemyState.Attack:  Attack();  break;
            case EnemyState.Stunned: break;
            case EnemyState.Dead:    break;
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (newState == currentState) return;
        currentState = newState;
        stateTimer = 0f;
        enemy.SetState(newState);
    }

    private void Idle()
    {
        enemy.PatrolArea();
        enemy.DetectPlayer();

        if (enemy.GetState() == EnemyState.Chase || enemy.GetState() == EnemyState.Attack)
        {
            noPlayerTimer = 0f;
            isAtBoundary = false;
            ChangeState(enemy.GetState());
            return;
        }

        if (isAtBoundary)
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= boundaryPauseTime)
            {
                movingRight = !movingRight;
                FlipSprite(movingRight);
                isAtBoundary = false;
                ChangeState(EnemyState.Patrol);
            }
            return;
        }

        noPlayerTimer += Time.deltaTime;
        if (noPlayerTimer >= noPlayerWalkDelay)
        {
            noPlayerTimer = 0f;
            ChangeState(EnemyState.Patrol);
        }
    }

    private void Patrol()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() == EnemyState.Chase || enemy.GetState() == EnemyState.Attack)
        {
            noPlayerTimer = 0f;
            ChangeState(enemy.GetState());
            return;
        }

        float x = transform.position.x;
        if (movingRight && x >= rightBoundary)
        {
            isAtBoundary = true;
            ChangeState(EnemyState.Idle);
            return;
        }
        if (!movingRight && x <= leftBoundary)
        {
            isAtBoundary = true;
            ChangeState(EnemyState.Idle);
            return;
        }

        if (!HasGroundAhead(movingRight))
        {
            isAtBoundary = true;
            ChangeState(EnemyState.Idle);
            return;
        }

        Vector2 direction = new Vector2(movingRight ? 1f : -1f, 0f);
        enemy.Patrol(direction, patrolSpeed);
        FlipSprite(movingRight);
    }

    private void Chase()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() == EnemyState.Attack)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        if (enemy.GetState() != EnemyState.Chase)
        {
            noPlayerTimer = 0f;
            enemy.SetState(EnemyState.Idle);
            ChangeState(EnemyState.Idle);
            return;
        }

        bool playerRight = enemy.playerTransform.position.x > enemy.transform.position.x;
        if (HasGroundAhead(playerRight))
            enemy.ChasePlayer();
        FlipSprite(playerRight);
    }

    private void Attack()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() != EnemyState.Attack)
        {
            if (enemy.GetState() != EnemyState.Chase)
            {
                noPlayerTimer = 0f;
                enemy.SetState(EnemyState.Idle);
            }
            ChangeState(enemy.GetState() == EnemyState.Chase ? EnemyState.Chase : EnemyState.Idle);
            return;
        }

        stateTimer += Time.deltaTime;
        if (stateTimer >= 0.6f)
        {
            enemy.Attack();
            stateTimer = 0f;
        }
    }

    private bool HasGroundAhead(bool movingRight)
    {
        float leadingX;
        float originY;

        if (_bodyCollider != null)
        {
            Bounds b = _bodyCollider.bounds;
            leadingX = movingRight ? b.max.x + edgeCheckLookahead : b.min.x - edgeCheckLookahead;
            originY  = b.min.y + 0.05f;
        }
        else
        {
            float offset = movingRight ? edgeCheckLookahead : -edgeCheckLookahead;
            leadingX = transform.position.x + offset;
            originY  = transform.position.y;
        }

        RaycastHit2D hit = Physics2D.Raycast(new Vector2(leadingX, originY), Vector2.down, edgeCheckDepth, groundLayer);
        return hit.collider != null;
    }

    private void FlipSprite(bool faceRight)
    {
        if (enemy == null) return;
        Vector3 euler = enemy.transform.eulerAngles;
        euler.y = (faceRight != enemy.SpriteFacesLeft) ? 0f : 180f;
        enemy.transform.eulerAngles = euler;
    }

    private void OnDrawGizmosSelected()
    {
        if (enemy == null) return;
        Collider2D col = _bodyCollider != null ? _bodyCollider : enemy.GetComponent<Collider2D>();

        float leadingX, originY;
        if (col != null)
        {
            Bounds b = col.bounds;
            leadingX = movingRight ? b.max.x + edgeCheckLookahead : b.min.x - edgeCheckLookahead;
            originY  = b.min.y + 0.05f;
        }
        else
        {
            float offset = movingRight ? edgeCheckLookahead : -edgeCheckLookahead;
            leadingX = transform.position.x + offset;
            originY  = transform.position.y;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(leadingX, originY, 0f), new Vector3(leadingX, originY - edgeCheckDepth, 0f));
    }
}
