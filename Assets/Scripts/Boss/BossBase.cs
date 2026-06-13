using UnityEngine;

public abstract class BossBase : MonoBehaviour
{
    [Header("Boss Stats")]
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected float detectionRadius = 8f;

    protected int currentHealth;
    protected BossStateBase currentState;
    protected Transform playerTransform;

    private bool _wasPlayerInRange;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        if (GetComponent<Rigidbody2D>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (GetComponent<CircleCollider2D>() == null)
        {
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = detectionRadius;
            col.isTrigger = true;
        }

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
    }

    protected virtual void Update()
    {
        currentState?.StateUpdate();
        PollDetection();
    }

    public void TransitionTo(BossStateBase newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    private void PollDetection()
    {
        if (playerTransform == null) return;

        bool inRange = Vector2.Distance(transform.position, playerTransform.position) <= detectionRadius;

        if (inRange && !_wasPlayerInRange)
        {
            _wasPlayerInRange = true;
            OnPlayerDetected();
        }
        else if (!inRange && _wasPlayerInRange)
        {
            _wasPlayerInRange = false;
            OnPlayerLost();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !_wasPlayerInRange)
        {
            playerTransform = other.transform;
            _wasPlayerInRange = true;
            OnPlayerDetected();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && _wasPlayerInRange)
        {
            _wasPlayerInRange = false;
            OnPlayerLost();
        }
    }

    public bool IsPlayerInRange(float range)
    {
        if (playerTransform == null) return false;
        return Vector2.Distance(transform.position, playerTransform.position) <= range;
    }

    public abstract void OnPlayerDetected();
    public abstract void OnPlayerLost();
    public abstract void TakeDamage(int amount);

    // --- Gizmos ---

    private void OnDrawGizmos()
    {
        // Subtle fill — always visible in Scene view
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.07f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    private void OnDrawGizmosSelected()
    {
        // Bright wire when object is selected: red = player detected, green = idle
        Gizmos.color = _wasPlayerInRange
            ? new Color(1f, 0.15f, 0.15f, 1f)
            : new Color(0.15f, 1f, 0.15f, 1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Line to player so you can see the measured distance
        if (playerTransform != null)
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }
}
