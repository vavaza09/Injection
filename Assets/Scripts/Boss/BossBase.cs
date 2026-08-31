using UnityEngine;

public abstract class BossBase : MonoBehaviour
{
    [Header("Boss Stats")]
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected float detectionRadius = 8f;

    protected int currentHealth;
    protected BossStateBase currentState;
    protected Transform playerTransform;

    public event System.Action<float> HealthChanged;
    public event System.Action PlayerEnteredRange;
    public event System.Action PlayerExitedRange;

    public static event System.Action Defeated;

    public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    protected static void Make3D(AudioSource src, float maxDist = 40f)
    {
        if (src == null) return;
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Linear;
        src.minDistance  = 1f;
        src.maxDistance  = maxDist;
    }
    public bool IsPlayerCurrentlyInRange => _wasPlayerInRange;

    protected void RaiseHealthChanged() => HealthChanged?.Invoke(HealthPercent);

    protected void RaiseDefeated()
    {
        if (Defeated == null) return;
        foreach (var del in Defeated.GetInvocationList())
        {
            try { ((System.Action)del).Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

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
            PlayerEnteredRange?.Invoke();
        }
        else if (!inRange && _wasPlayerInRange)
        {
            _wasPlayerInRange = false;
            OnPlayerLost();
            PlayerExitedRange?.Invoke();
        }
    }

    // Detection is handled solely by PollDetection() (distance vs detectionRadius).
    // Trigger-based enter/exit was removed: BossBase can't tell which of the boss's
    // many trigger colliders (weak points, attack zones, junk...) the player touched,
    // so it fired spurious PlayerExited/Entered every time the player crossed one,
    // making the health-bar reveal and idle/aggro states thrash.

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
        // Wire only — solid DrawSphere is re-tessellated every SceneView repaint
        // and 3 of these overlap on the boss GO (BossBase + Boss + BossAttackManager).
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
