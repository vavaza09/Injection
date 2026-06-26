using UnityEngine;

public class MeleeEnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float walkDistance = 5f;

    private Vector2 startPosition;
    private int direction = 1;
    private Rigidbody2D rb;

    public int Facing => direction;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
    }

    public void Patrol()
    {
        // Move enemy
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * patrolSpeed, rb.linearVelocity.y);
        }

        // Check bounds
        float distance = transform.position.x - startPosition.x;

        if (distance > walkDistance)
        {
            direction = -1;
            Flip();
        }
        else if (distance < -walkDistance)
        {
            direction = 1;
            Flip();
        }
    }

    public void ChaseTarget(Vector3 targetPos)
    {
        if (targetPos.x > transform.position.x)
        {
            direction = 1;
            Flip();
        }
        else
        {
            direction = -1;
            Flip();
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * chaseSpeed, rb.linearVelocity.y);
        }
    }

    public void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    private void Flip()
    {
        Vector3 euler = transform.eulerAngles;
        euler.y = (direction > 0) ? 0f : 180f;
        transform.eulerAngles = euler;
    }
}