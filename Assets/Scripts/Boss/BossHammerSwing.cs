using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Horizontal hammer swing attack for a 2D sprite-based boss.
// Drives the arm via two IK-related Transforms (no direct bone rotation):
//   upperLeft  = Boss_Upper_L                       (IK chain root — shoulder follow-through)
//   hammerLeft = Boss_Arm_Left_LimbSolver2D_Target  (IK effector target — drives the horizontal sweep)
//
// The LimbSolver2D resolves arm bones to reach hammerLeft each frame automatically.
// Attach to the Boss root. Auto-resolves targets at Start; assign in Inspector to override.
public class BossHammerSwing : MonoBehaviour
{
    // ── IK Targets ────────────────────────────────────────────────────────

    [Header("IK Targets")]
    [Tooltip("Boss_Upper_L — IK chain root. Small counter-motion gives shoulder follow-through.")]
    [SerializeField] private Transform upperLeft;

    [Tooltip("Boss_Arm_Left_LimbSolver2D_Target — IK effector target. This is what you swing.")]
    [SerializeField] private Transform hammerLeft;

    // ── Collider ──────────────────────────────────────────────────────────

    [Header("Hammer Collider")]
    [Tooltip("'Hammer collider' BoxCollider2D — enabled only during the active hold phase.")]
    [SerializeField] private BoxCollider2D hammerCollider;

    // ── Offsets ───────────────────────────────────────────────────────────

    [Header("Swing Offsets  (local space)")]
    [Tooltip("MAIN SWING: applied to hammerLeft local position.\n" +
             "Positive X = forward toward player.  Y near zero = horizontal (not a slam).\n" +
             "Default (2, -0.2) keeps the hammer at roughly the same height as idle.")]
    [SerializeField] private Vector2 swingTargetOffset = new Vector2(2f, -0.2f);

    [Tooltip("PRE-SWING pull-back: applied to upperLeft local position before the swing.\n" +
             "Negative X = pull shoulder back slightly. Set to (0,0) to skip wind-up entirely.")]
    [SerializeField] private Vector2 windUpOffset = new Vector2(-0.3f, 0f);

    // ── Timing ────────────────────────────────────────────────────────────

    [Header("Timing  (seconds)")]
    [SerializeField] private float swingDuration   = 0.3f;
    [SerializeField] private float holdDuration    = 0.3f;
    [SerializeField] private float recoverDuration = 0.8f;

    // ── Range & Cooldown ──────────────────────────────────────────────────

    [Header("Range & Cooldown")]
    [SerializeField] private float attackRange = 15f;
    [SerializeField] private float cooldown    = 2f;

    // ── Impact Event ──────────────────────────────────────────────────────

    [Header("Impact")]
    [Tooltip("Fires at the moment the swing reaches the target. Hook up VFX, SFX, and damage here.")]
    public UnityEvent onImpact;

    // ── Runtime state (read-only from Inspector) ──────────────────────────

    [Header("Runtime State  (read-only)")]
    [SerializeField] private bool isAttacking;
    public bool IsAttacking => isAttacking;

    // ── Private ───────────────────────────────────────────────────────────

    private Vector3   _upperRestLocal;
    private Vector3   _hammerRestLocal;
    private bool      _restCaptured;
    private Transform _player;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        ResolveReferences();

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        if (hammerCollider != null) hammerCollider.enabled = false;
    }

    private void Update()
    {
        if (isAttacking || _player == null) return;
        if (Vector2.Distance(transform.position, _player.position) <= attackRange)
            StartCoroutine(SwingRoutine());
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Trigger the attack from external scripts or the editor button.</summary>
    public void TriggerAttack()
    {
        if (!isAttacking) StartCoroutine(SwingRoutine());
    }

    /// <summary>Immediately snap the arm back to rest (use on interruption or boss death).</summary>
    public void ResetArm()
    {
        StopAllCoroutines();
        isAttacking = false;
        if (hammerCollider != null) hammerCollider.enabled = false;

        if (_restCaptured)
        {
            if (upperLeft  != null) upperLeft.localPosition  = _upperRestLocal;
            if (hammerLeft != null) hammerLeft.localPosition = _hammerRestLocal;
        }
    }

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator SwingRoutine()
    {
        isAttacking = true;
        CaptureRest();

        Vector3 windUpPos = _upperRestLocal  + (Vector3)windUpOffset;
        Vector3 swingPos  = _hammerRestLocal + (Vector3)swingTargetOffset;

        // PRE-SWING WIND-UP (optional — skipped if windUpOffset is zero)
        // Shoulder pulls back very quickly to "load" the swing
        if (windUpOffset.sqrMagnitude > 0.0001f)
            yield return TweenLocal(upperLeft, _upperRestLocal, windUpPos, swingDuration * 0.3f, EaseOut);

        // HORIZONTAL SWING
        // hammerLeft sweeps forward with EaseIn (builds momentum into the hit)
        // upperLeft simultaneously follows through back to rest with EaseOut
        Vector3 upperSwingFrom = upperLeft != null ? upperLeft.localPosition : _upperRestLocal;
        yield return SwingPhase(upperSwingFrom, swingPos, swingDuration);

        // IMPACT & HOLD
        if (hammerCollider != null) hammerCollider.enabled = true;
        onImpact?.Invoke();
        yield return new WaitForSeconds(holdDuration);
        if (hammerCollider != null) hammerCollider.enabled = false;

        // RECOVERY — both targets return to rest simultaneously
        Vector3 upperAtImpact = upperLeft  != null ? upperLeft.localPosition  : _upperRestLocal;
        Vector3 hammerAtImpact = hammerLeft != null ? hammerLeft.localPosition : _hammerRestLocal;
        yield return RecoverAll(upperAtImpact, hammerAtImpact);

        isAttacking = false;
        yield return new WaitForSeconds(cooldown);
    }

    // Simultaneously sweeps hammerLeft forward (EaseIn) and returns upperLeft to rest (EaseOut)
    private IEnumerator SwingPhase(Vector3 upperFrom, Vector3 hammerTo, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (upperLeft  != null) upperLeft.localPosition  = Vector3.Lerp(upperFrom,        _upperRestLocal, EaseOut(t));
            if (hammerLeft != null) hammerLeft.localPosition = Vector3.Lerp(_hammerRestLocal, hammerTo,        EaseIn(t));
            yield return null;
        }
        if (upperLeft  != null) upperLeft.localPosition  = _upperRestLocal;
        if (hammerLeft != null) hammerLeft.localPosition = hammerTo;
    }

    private IEnumerator RecoverAll(Vector3 fromUpper, Vector3 fromHammer)
    {
        float elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float c = SmoothStep(Mathf.Clamp01(elapsed / recoverDuration));
            if (upperLeft  != null) upperLeft.localPosition  = Vector3.Lerp(fromUpper,  _upperRestLocal,  c);
            if (hammerLeft != null) hammerLeft.localPosition = Vector3.Lerp(fromHammer, _hammerRestLocal, c);
            yield return null;
        }
        if (upperLeft  != null) upperLeft.localPosition  = _upperRestLocal;
        if (hammerLeft != null) hammerLeft.localPosition = _hammerRestLocal;
    }

    private static IEnumerator TweenLocal(
        Transform t, Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
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

    private void ResolveReferences()
    {
        if (upperLeft  == null) upperLeft  = FindDeep("Boss_Upper_L");
        if (hammerLeft == null) hammerLeft = FindDeep("Boss_Arm_Left_LimbSolver2D_Target")
                                          ?? FindScene("Boss_Arm_Left_LimbSolver2D_Target");

        if (hammerCollider == null)
        {
            var go = GameObject.Find("Hammer collider");
            if (go != null) hammerCollider = go.GetComponent<BoxCollider2D>();
        }

        if (upperLeft      == null) Debug.LogWarning("[BossHammerSwing] 'Boss_Upper_L' not found — assign Upper Left in Inspector.");
        if (hammerLeft     == null) Debug.LogWarning("[BossHammerSwing] 'Boss_Arm_Left_LimbSolver2D_Target' not found — assign Hammer Left in Inspector.");
        if (hammerCollider == null) Debug.LogWarning("[BossHammerSwing] 'Hammer collider' not found — assign Hammer Collider in Inspector.");
    }

    private void CaptureRest()
    {
        if (_restCaptured) return;
        _upperRestLocal  = upperLeft  != null ? upperLeft.localPosition  : Vector3.zero;
        _hammerRestLocal = hammerLeft != null ? hammerLeft.localPosition : Vector3.zero;
        _restCaptured    = true;
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
        // Attack-range ring (cyan tint to distinguish from slam)
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.12f);
        Gizmos.DrawSphere(transform.position, attackRange);
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (hammerLeft == null) return;

        Vector3 idleWorld = hammerLeft.position;
        Vector3 swingWorld = LocalOffsetToWorld(hammerLeft, (Vector3)swingTargetOffset);

        // Horizontal guide — shows Y is barely changing
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
        Gizmos.DrawLine(
            new Vector3(idleWorld.x - 0.5f,  idleWorld.y, idleWorld.z),
            new Vector3(swingWorld.x + 0.5f, idleWorld.y, idleWorld.z));

        // Swing path line
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(idleWorld, swingWorld);

        // Swing target sphere (bigger than idle to show the endpoint clearly)
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 1f);
        Gizmos.DrawSphere(swingWorld, 0.4f);

        // Wind-up shoulder position (yellow, smaller)
        if (upperLeft != null && windUpOffset.sqrMagnitude > 0.0001f)
        {
            Vector3 windUpWorld = LocalOffsetToWorld(upperLeft, (Vector3)windUpOffset);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Gizmos.DrawSphere(windUpWorld, 0.2f);
            Gizmos.DrawLine(upperLeft.position, windUpWorld);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(0.2f, 0.85f, 1f, 1f);
        UnityEditor.Handles.Label(swingWorld + Vector3.up * 0.5f, "Swing target");

        if (upperLeft != null && windUpOffset.sqrMagnitude > 0.0001f)
        {
            Vector3 windUpWorld = LocalOffsetToWorld(upperLeft, (Vector3)windUpOffset);
            UnityEditor.Handles.color = new Color(1f, 0.9f, 0.2f, 1f);
            UnityEditor.Handles.Label(windUpWorld + Vector3.up * 0.4f, "Wind-up");
        }
#endif
    }

    private static Vector3 LocalOffsetToWorld(Transform t, Vector3 localOffset)
    {
        return t.parent != null
            ? t.parent.TransformPoint(t.localPosition + localOffset)
            : t.position + localOffset;
    }
}
