using UnityEngine;

public class MeleeEnemyMovement : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;

    [Header("Patrol Settings")]
    [SerializeField] private float patrolDistance = 5f;
    [SerializeField] private float pauseAtEdgeDuration = 0.5f;
    [SerializeField] private float edgeDetectionDistance = 1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.2f;

    // Private variables
    private Rigidbody2D rb;
    private Vector2 patrolStartPosition;
    private float patrolPauseTimer;
    private bool isPaused = false;
    private int facing = 1;
    private float currentSpeed;

    // Public properties
    public int Facing => facing;
    public bool IsPaused => isPaused;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        patrolStartPosition = transform.position;
        currentSpeed = patrolSpeed;
    }

    public void Patrol()
    {
        currentSpeed = patrolSpeed;

        // Handle pause state
        if (isPaused)
        {
            patrolPauseTimer -= Time.deltaTime;
            if (patrolPauseTimer <= 0f)
            {
                isPaused = false;
                // Turn around after pause
                facing *= -1;
                FlipSprite();
            }

            StopMovement();
            return;
        }

        // Check for edge ahead (ground detection)
        Vector2 edgeCheckPosition = (Vector2)transform.position + new Vector2(facing * edgeDetectionDistance, 0f);
        RaycastHit2D groundCheck = Physics2D.Raycast(edgeCheckPosition, Vector2.down, edgeDetectionDistance, groundLayer);

        // Check for wall ahead
        Vector2 wallCheckPosition = (Vector2)transform.position + new Vector2(0, 0.5f);
        RaycastHit2D wallCheck = Physics2D.Raycast(wallCheckPosition, new Vector2(facing, 0), edgeDetectionDistance * 0.5f, groundLayer);

        // If no ground ahead or hit a wall, pause and turn
        if (!groundCheck.collider || wallCheck.collider)
        {
            InitiatePause();
            return;
        }

        // Check if reached patrol distance limit from start position
        float distanceFromStart = Mathf.Abs(transform.position.x - patrolStartPosition.x);
        if (distanceFromStart >= patrolDistance)
        {
            InitiatePause();
            return;
        }

        // Move in current direction
        MoveInDirection(new Vector2(facing, 0));
    }

    /// <summary>
    /// Chase the target at higher speed
    /// </summary>
    public void ChaseTarget(Vector3 targetPosition)
    {
        currentSpeed = chaseSpeed;

        Vector2 direction = targetPosition - transform.position;

        // Update facing direction
        if (direction.x > 0 && facing != 1)
        {
            facing = 1;
            FlipSprite();
        }
        else if (direction.x < 0 && facing != -1)
        {
            facing = -1;
            FlipSprite();
        }

        // Move only horizontally (like Hollow Knight)
        direction.y = 0f;
        MoveInDirection(direction.normalized);
    }

    /// <summary>
    /// Move in a specific direction at current speed
    /// </summary>
    public void MoveInDirection(Vector2 direction)
    {
        if (rb != null)
        {
            Vector2 velocity = direction * currentSpeed;
            velocity.y = rb.linearVelocity.y; // Preserve vertical velocity (gravity)
            rb.linearVelocity = velocity;
        }
        else
        {
            transform.position += (Vector3)(direction * currentSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Stop all horizontal movement
    /// </summary>
    public void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    /// <summary>
    /// Face towards a target position
    /// </summary>
    public void FaceTarget(Vector3 targetPosition)
    {
        float direction = targetPosition.x - transform.position.x;

        if (direction > 0 && facing != 1)
        {
            facing = 1;
            FlipSprite();
        }
        else if (direction < 0 && facing != -1)
        {
            facing = -1;
            FlipSprite();
        }
    }

    /// <summary>
    /// Manually set facing direction
    /// </summary>
    public void SetFacing(int newFacing)
    {
        if (newFacing != facing)
        {
            facing = newFacing;
            FlipSprite();
        }
    }

    /// <summary>
    /// Check if enemy is grounded
    /// </summary>
    public bool IsGrounded()
    {
        if (groundCheckPoint != null)
        {
            return Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            Vector2 checkPosition = (Vector2)transform.position + Vector2.down * 0.1f;
            return Physics2D.OverlapCircle(checkPosition, groundCheckRadius, groundLayer);
        }
    }

    /// <summary>
    /// Apply a force (for knockback, jump attacks, etc.)
    /// </summary>
    public void ApplyForce(Vector2 force, ForceMode2D mode = ForceMode2D.Impulse)
    {
        if (rb != null)
        {
            rb.AddForce(force, mode);
        }
    }

    /// <summary>
    /// Set velocity directly
    /// </summary>
    public void SetVelocity(Vector2 velocity)
    {
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    private void InitiatePause()
    {
        isPaused = true;
        patrolPauseTimer = pauseAtEdgeDuration;
        StopMovement();
    }

    private void FlipSprite()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
    }

    // Visual debugging in editor
    private void OnDrawGizmosSelected()
    {
        // Draw patrol area
        Gizmos.color = Color.blue;
        Vector3 startPos = Application.isPlaying ? (Vector3)patrolStartPosition : transform.position;
        Gizmos.DrawLine(startPos + Vector3.left * patrolDistance, startPos + Vector3.right * patrolDistance);

        // Draw edge detection
        Gizmos.color = Color.green;
        int drawFacing = Application.isPlaying ? facing : 1;
        Vector3 edgeCheckStart = transform.position + new Vector3(drawFacing * edgeDetectionDistance, 0);
        Vector3 edgeCheckEnd = edgeCheckStart + Vector3.down * edgeDetectionDistance;
        Gizmos.DrawLine(edgeCheckStart, edgeCheckEnd);

        // Draw wall detection
        Gizmos.color = Color.yellow;
        Vector3 wallCheckStart = transform.position + Vector3.up * 0.5f;
        Vector3 wallCheckEnd = wallCheckStart + new Vector3(drawFacing * edgeDetectionDistance * 0.5f, 0);
        Gizmos.DrawLine(wallCheckStart, wallCheckEnd);

        // Draw ground check point
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}