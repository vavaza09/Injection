using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Self-contained hammer slam attack for a 2D sprite-based boss.
// Drives animation by moving two IK-related Transforms in local space:
//   upperLeft  = Boss_Upper_L              (IK chain root  — wind-up shoulder motion)
//   hammerLeft = the LimbSolver2D Target   (IK effector    — slam arc endpoint)
//
// The LimbSolver2D adjusts the arm bones every frame to reach hammerLeft,
// so you only need to move these two handles and the arm follows automatically.
//
// Attach to the Boss root GameObject. Assign the two IK targets in the Inspector,
// or let ResolveTargets() find them by name at Start.
public class CrabHammerSlam : MonoBehaviour
{
    [Header("IK Targets")]
    [Tooltip("Boss_Upper_L — IK chain root. Moving this raises/pulls the arm base during wind-up.")]
    [SerializeField] private Transform upperLeft;

    [Tooltip("Boss_Arm_Left_LimbSolver2D_Target — IK effector target. The solver drives the arm to reach this.")]
    [SerializeField] private Transform hammerLeft;

    [Header("Wind-up  (~0.4 s)")]
    [Tooltip("Local-space offset from upperLeft rest position. Negative X = pull back; positive Y = raise up.")]
    [SerializeField] private Vector2 windUpOffset = new Vector2(-0.5f, 1f);
    [SerializeField] private float windUpTime = 0.4f;

    [Header("Forward Swing → Slam  (~0.3 s)")]
    [Tooltip("Local-space offset from hammerLeft rest position. Positive X = forward toward player; negative Y = downward.")]
    [SerializeField] private Vector2 slamOffset = new Vector2(3f, -5f);
    [SerializeField] private float slamTime = 0.3f;
    [SerializeField] private float holdTime = 0.1f;

    [Header("Recovery  (~0.8 s)")]
    [SerializeField] private float recoverTime = 0.8f;

    [Header("Range & Cooldown")]
    [SerializeField] private float attackRange = 15f;
    [SerializeField] private float cooldown    = 2f;

    [Header("Impact Event")]
    [Tooltip("Fires at the moment of ground impact. Wire up VFX, SFX, or damage spawning here.")]
    public UnityEvent onImpact;

    // ── State ─────────────────────────────────────────────────────────────

    public bool IsAttacking { get; private set; }

    private Vector3   _upperRestLocal;
    private Vector3   _hammerRestLocal;
    private bool      _restCaptured;
    private Transform _player;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
        ResolveTargets();
    }

    private void Update()
    {
        if (IsAttacking || _player == null) return;
        if (Vector2.Distance(transform.position, _player.position) <= attackRange)
            StartCoroutine(AttackRoutine());
    }

    // ── Public API ────────────────────────────────────────────────────────

    // Call from any external script (e.g. Boss state machine) to trigger the attack.
    public void TriggerAttack()
    {
        if (!IsAttacking) StartCoroutine(AttackRoutine());
    }

    // Snap the arm back to rest immediately (boss death, interrupt, etc.).
    public void ResetArm()
    {
        StopAllCoroutines();
        IsAttacking = false;
        if (_restCaptured)
        {
            if (upperLeft  != null) upperLeft.localPosition  = _upperRestLocal;
            if (hammerLeft != null) hammerLeft.localPosition = _hammerRestLocal;
        }
    }

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;
        CaptureRest();

        Vector3 windUp  = _upperRestLocal  + (Vector3)windUpOffset;
        Vector3 slamPos = _hammerRestLocal + (Vector3)slamOffset;

        // WIND-UP: arm base sweeps slightly up and back
        // ease-out: decelerates smoothly into the held wind-up pose
        yield return TweenLocal(upperLeft, _upperRestLocal, windUp, windUpTime, EaseOut);

        // FORWARD SWING → GROUND SLAM: IK target arcs forward and downward
        // ease-in: accelerates into the floor, giving a heavy-impact feel
        yield return TweenLocal(hammerLeft, _hammerRestLocal, slamPos, slamTime, EaseIn);

        onImpact?.Invoke();
        yield return new WaitForSeconds(holdTime);

        // RECOVERY: both handles return to rest simultaneously with smooth-step
        yield return RecoverAll(windUp, slamPos);

        IsAttacking = false;
        yield return new WaitForSeconds(cooldown);
    }

    private void CaptureRest()
    {
        if (_restCaptured) return;
        _upperRestLocal  = upperLeft  != null ? upperLeft.localPosition  : Vector3.zero;
        _hammerRestLocal = hammerLeft != null ? hammerLeft.localPosition : Vector3.zero;
        _restCaptured    = true;
    }

    private IEnumerator RecoverAll(Vector3 fromUpper, Vector3 fromHammer)
    {
        float elapsed = 0f;
        while (elapsed < recoverTime)
        {
            elapsed += Time.deltaTime;
            float c = SmoothStep(Mathf.Clamp01(elapsed / recoverTime));
            if (upperLeft  != null) upperLeft.localPosition  = Vector3.Lerp(fromUpper,  _upperRestLocal,  c);
            if (hammerLeft != null) hammerLeft.localPosition = Vector3.Lerp(fromHammer, _hammerRestLocal, c);
            yield return null;
        }
        // Snap to exact rest to prevent float drift
        if (upperLeft  != null) upperLeft.localPosition  = _upperRestLocal;
        if (hammerLeft != null) hammerLeft.localPosition = _hammerRestLocal;
    }

    // ── Tween helper ──────────────────────────────────────────────────────

    private static IEnumerator TweenLocal(
        Transform t, Vector3 from, Vector3 to,
        float duration, System.Func<float, float> ease)
    {
        if (t == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            t.localPosition = Vector3.Lerp(from, to, ease(Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        t.localPosition = to;
    }

    // ── Reference resolution ──────────────────────────────────────────────

    private void ResolveTargets()
    {
        // Boss_Upper_L is a child of Boss — search descendants
        if (upperLeft  == null) upperLeft  = FindDeep("Boss_Upper_L");

        // Boss_Arm_Left_LimbSolver2D_Target lives under IK_Manager (not under Boss)
        // so fall back to a scene-wide search
        if (hammerLeft == null) hammerLeft = FindDeep("Boss_Arm_Left_LimbSolver2D_Target")
                                          ?? FindScene("Boss_Arm_Left_LimbSolver2D_Target");

        if (upperLeft  == null) Debug.LogWarning("[CrabHammerSlam] 'upper left' (Boss_Upper_L) not found — assign in Inspector.");
        if (hammerLeft == null) Debug.LogWarning("[CrabHammerSlam] 'Hammer left' (Boss_Arm_Left_LimbSolver2D_Target) not found — assign in Inspector.");
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

    // ── Easing ────────────────────────────────────────────────────────────

    private static float EaseIn(float t)     => t * t;
    private static float EaseOut(float t)    => 1f - (1f - t) * (1f - t);
    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

    // ── Gizmos ────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Attack-range indicator
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.12f);
        Gizmos.DrawSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (upperLeft == null || hammerLeft == null) return;

        // Wind-up position (yellow) — where upperLeft moves to
        Vector3 windUpWorld = LocalOffsetToWorld(upperLeft,  (Vector3)windUpOffset);
        // Slam position (red) — where hammerLeft moves to
        Vector3 slamWorld   = LocalOffsetToWorld(hammerLeft, (Vector3)slamOffset);

        // Wind-up sphere + line from current arm root
        Gizmos.color = new Color(1f, 0.9f, 0f, 1f);
        Gizmos.DrawSphere(windUpWorld, 0.25f);
        Gizmos.DrawLine(upperLeft.position, windUpWorld);

        // Slam sphere + line from current hammer target
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 1f);
        Gizmos.DrawSphere(slamWorld, 0.4f);
        Gizmos.DrawLine(hammerLeft.position, slamWorld);

        // Arc: quadratic bezier approximating the hammer's swing path
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.75f);
        DrawSwingArc(hammerLeft.position, slamWorld, 12);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0f, 0.9f);
        UnityEditor.Handles.Label(windUpWorld + Vector3.up * 0.5f, "Wind-up");
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        UnityEditor.Handles.Label(slamWorld   + Vector3.up * 0.5f, "Slam");
#endif
    }

    // Converts a local-space offset on a Transform to world space correctly.
    private static Vector3 LocalOffsetToWorld(Transform t, Vector3 localOffset)
    {
        return t.parent != null
            ? t.parent.TransformPoint(t.localPosition + localOffset)
            : t.position + localOffset;
    }

    // Quadratic bezier arc: lifts the mid-point slightly to suggest a downward swing.
    private static void DrawSwingArc(Vector3 from, Vector3 to, int steps)
    {
        Vector3 mid  = (from + to) * 0.5f + Vector3.up * 1.2f;
        Vector3 prev = from;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = 1f - t;
            Vector3 pt = u * u * from + 2f * u * t * mid + t * t * to;
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }
    }
}
