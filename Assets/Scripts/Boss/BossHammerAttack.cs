using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Self-contained hammer swing attack for a 2D sprite-based boss.
//
// Detection: a CircleCollider2D (isTrigger) on the boss detects the Player tag.
//   → Player enters  : starts the attack loop automatically.
//   → Player exits   : stops initiating new attacks (current swing completes normally).
//
// Attack arc: the hammer IK target follows a quadratic bezier curve, not a straight line,
// so the arm actually swings through an arc rather than sliding linearly.
//
// Phases
//   1. Wind-up  (~0.5 s) : IK target rises up and slightly back (ease-in).
//   2. Swing    (~0.25 s): bezier arc forward and down to impact point (sharp ease-in).
//   3. Hold     (~0.3 s) : hammer stays at impact, collider active, OnImpact fires.
//   4. Recovery (~0.8 s) : bezier arc gently back to idle position (ease-out).
//
// Attach to the boss root. Auto-finds IK targets and Hammer collider by name on Start.
// Call TriggerAttack() from external code if you want to fire the attack manually too.
public class BossHammerAttack : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("'upper left' — upper-arm IK root. Idle position saved at Start.")]
    [SerializeField] private Transform upperLeftIKTarget;

    [Tooltip("'Hammer left' — IK effector target. This drives the entire swing arc.")]
    [SerializeField] private Transform hammerLeftIKTarget;

    [Tooltip("'Hammer collider' GameObject — activated during impact hold only.")]
    [SerializeField] private GameObject hammerCollider;

    // ── Detection ─────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Radius of the CircleCollider2D trigger used to detect the player.\n" +
             "Make sure there is a CircleCollider2D (isTrigger) on this GameObject.")]
    [SerializeField] private float detectionRadius = 5f;

    // ── Wind-Up Phase ─────────────────────────────────────────────────────

    [Header("Wind-Up Phase")]
    [Tooltip("Offset from the saved idle world position. Negative X = back, positive Y = raise up.")]
    [SerializeField] private Vector2 windUpOffset = new Vector2(-0.5f, 2.5f);
    [SerializeField] private float windUpDuration = 0.5f;
    [Tooltip("Ease-in: starts slow, accelerates into the raised pose.")]
    [SerializeField] private AnimationCurve windUpCurve;

    // ── Swing Phase ───────────────────────────────────────────────────────

    [Header("Swing Phase")]
    [Tooltip("Bezier control point offset — the arc passes near here during the swing.\n" +
             "Positive X = forward, positive Y = still high (creates the overhead arc).")]
    [SerializeField] private Vector2 swingArcMidOffset = new Vector2(0.5f, 1f);

    [Tooltip("Arc endpoint offset — where the hammer hits the ground.\n" +
             "Positive X = forward toward player, negative Y = down into the ground.")]
    [SerializeField] private Vector2 swingImpactOffset = new Vector2(1.5f, -1.5f);
    [SerializeField] private float swingDuration = 0.25f;
    [Tooltip("Sharp ease-in: explosive start, decelerates at impact — feels heavy and powerful.")]
    [SerializeField] private AnimationCurve swingCurve;

    // ── Impact Hold ───────────────────────────────────────────────────────

    [Header("Impact Hold")]
    [SerializeField] private float holdDuration = 0.3f;
    [Tooltip("Fires at the moment of impact. Wire up screenshake, VFX, SFX, and damage here.")]
    public UnityEvent OnImpact;

    // ── Recovery Phase ────────────────────────────────────────────────────

    [Header("Recovery Phase")]
    [Tooltip("Bezier control point for the return arc — gives the lift a natural curve.")]
    [SerializeField] private Vector2 recoveryArcMidOffset = new Vector2(0.3f, 0.5f);
    [SerializeField] private float recoveryDuration = 0.8f;
    [Tooltip("Ease-out: starts moving, gently decelerates back to idle.")]
    [SerializeField] private AnimationCurve recoveryCurve;

    // ── Settings ──────────────────────────────────────────────────────────

    [Header("Settings")]
    [SerializeField] private float cooldown = 2f;
    [Tooltip("Read-only — shows attack state in the Inspector during Play Mode.")]
    [SerializeField] private bool isAttacking;
    public bool IsAttacking => isAttacking;

    // ── Private state ─────────────────────────────────────────────────────

    private bool    _playerInRange;
    private Vector3 _idleHammerPos;
    private Vector3 _idleUpperPos;
    private bool    _idleSaved;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureDefaultCurves();
        ResolveReferences();
    }

    private void Start()
    {
        SaveIdlePositions();
        if (hammerCollider != null) hammerCollider.SetActive(false);
    }

    private void Update()
    {
        if (_playerInRange && !isAttacking)
            StartCoroutine(AttackRoutine());
    }

    // ── Trigger detection ─────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = false;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Start the attack immediately (also callable from external AI/state machine).</summary>
    public void TriggerAttack()
    {
        if (!isAttacking) StartCoroutine(AttackRoutine());
    }

    /// <summary>Abort mid-attack and snap the arm back to idle (death, stagger, etc.).</summary>
    public void ResetArm()
    {
        StopAllCoroutines();
        isAttacking    = false;
        _playerInRange = false;
        if (hammerCollider != null) hammerCollider.SetActive(false);
        if (!_idleSaved) return;
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = _idleHammerPos;
        if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = _idleUpperPos;
    }

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        // Mirror X offsets when boss is facing left (negative lossy X scale).
        float xDir    = transform.lossyScale.x >= 0f ? 1f : -1f;
        Vector3 windUp = _idleHammerPos + new Vector3(windUpOffset.x       * xDir, windUpOffset.y,       0f);
        Vector3 arcMid = _idleHammerPos + new Vector3(swingArcMidOffset.x  * xDir, swingArcMidOffset.y,  0f);
        Vector3 impact = _idleHammerPos + new Vector3(swingImpactOffset.x  * xDir, swingImpactOffset.y,  0f);
        Vector3 recMid = _idleHammerPos + new Vector3(recoveryArcMidOffset.x * xDir, recoveryArcMidOffset.y, 0f);

        // Phase 1 — Wind-up: arm rises to loaded position
        yield return TweenWorld(hammerLeftIKTarget, _idleHammerPos, windUp, windUpDuration, windUpCurve);

        // Phase 2 — Swing: arc from raised position through overshoot → ground impact
        yield return BezierTween(hammerLeftIKTarget, windUp, arcMid, impact, swingDuration, swingCurve);

        // Phase 3 — Impact hold: collider on, event fires, hammer stays at ground
        if (hammerCollider != null) hammerCollider.SetActive(true);
        OnImpact?.Invoke();
        yield return new WaitForSeconds(holdDuration);

        // Phase 4 — Recovery: collider off, arc back to idle
        if (hammerCollider != null) hammerCollider.SetActive(false);
        yield return BezierTween(hammerLeftIKTarget, impact, recMid, _idleHammerPos, recoveryDuration, recoveryCurve);

        isAttacking = false;
        yield return new WaitForSeconds(cooldown);
    }

    // ── Tween helpers ─────────────────────────────────────────────────────

    private static IEnumerator TweenWorld(
        Transform t, Vector3 from, Vector3 to, float dur, AnimationCurve curve)
    {
        if (t == null) yield break;
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float raw = Mathf.Clamp01(e / dur);
            t.position = Vector3.Lerp(from, to, curve != null ? curve.Evaluate(raw) : raw);
            yield return null;
        }
        t.position = to;
    }

    private static IEnumerator BezierTween(
        Transform t, Vector3 from, Vector3 control, Vector3 to, float dur, AnimationCurve curve)
    {
        if (t == null) yield break;
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float raw = Mathf.Clamp01(e / dur);
            t.position = QuadraticBezier(from, control, to, curve != null ? curve.Evaluate(raw) : raw);
            yield return null;
        }
        t.position = to;
    }

    /// <summary>Quadratic bezier: smoothly interpolates through a control point for a curved arc.</summary>
    private static Vector3 QuadraticBezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }

    // ── Setup helpers ─────────────────────────────────────────────────────

    private void SaveIdlePositions()
    {
        if (_idleSaved) return;
        _idleHammerPos = hammerLeftIKTarget != null ? hammerLeftIKTarget.position : Vector3.zero;
        _idleUpperPos  = upperLeftIKTarget  != null ? upperLeftIKTarget.position  : Vector3.zero;
        _idleSaved     = true;
    }

    private void ResolveReferences()
    {
        if (upperLeftIKTarget == null)
            upperLeftIKTarget = FindDeep("upper left") ?? FindDeep("Boss_Upper_L");

        if (hammerLeftIKTarget == null)
            hammerLeftIKTarget = FindDeep("Hammer left")
                              ?? FindDeep("Boss_Arm_Left_LimbSolver2D_Target")
                              ?? FindScene("Boss_Arm_Left_LimbSolver2D_Target");

        if (hammerCollider == null)
        {
            var t = FindDeep("Hammer collider") ?? FindScene("Hammer collider");
            if (t != null) hammerCollider = t.gameObject;
        }

        if (upperLeftIKTarget  == null) Debug.LogWarning("[BossHammerAttack] 'upper left' IK target not found — assign in Inspector.");
        if (hammerLeftIKTarget == null) Debug.LogWarning("[BossHammerAttack] 'Hammer left' IK target not found — assign in Inspector.");
        if (hammerCollider     == null) Debug.LogWarning("[BossHammerAttack] 'Hammer collider' not found — assign in Inspector.");
    }

    private Transform FindDeep(string n)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    private static Transform FindScene(string n)
    {
        var go = GameObject.Find(n);
        return go != null ? go.transform : null;
    }

    // Called by Unity when component is added via the Inspector — pre-fills curves.
    private void Reset()
    {
        windUpCurve   = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f),  new Keyframe(1f, 1f, 2f, 2f));
        swingCurve    = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f),  new Keyframe(1f, 1f, 0f, 0f));
        recoveryCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f),  new Keyframe(1f, 1f, 0f, 0f));
    }

    // Called in Awake — sets curve defaults when added via AddComponent (not via Inspector).
    private void EnsureDefaultCurves()
    {
        if (windUpCurve == null || windUpCurve.length == 0)
            windUpCurve   = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        if (swingCurve == null || swingCurve.length == 0)
            swingCurve    = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f), new Keyframe(1f, 1f, 0f, 0f));
        if (recoveryCurve == null || recoveryCurve.length == 0)
            recoveryCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }
}
