using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Hammer slam attack for a 2D sprite-based boss, driven by IK target Transforms.
// BOTH the shoulder (Boss_Upper_L) AND the end-effector (Boss_Arm_Left_LimbSolver2D_Target)
// move simultaneously so the entire arm extends during the slam.
// Detection and attack selection are handled by BossAttackManager — this script
// only runs the animation when TriggerAttack() is called.
public class BossHammerAttack : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Upper-arm IK root — idle position saved at Start.")]
    [SerializeField] private Transform upperLeftIKTarget;

    [Tooltip("Hammer IK effector target — drives the entire swing arc.")]
    [SerializeField] private Transform hammerLeftIKTarget;

    [Tooltip("'Hammer collider' GameObject — activated during impact hold only.")]
    [SerializeField] private GameObject hammerCollider;

    // ── Wind-Up Phase ─────────────────────────────────────────────────────────────

    [Header("Wind-Up Phase")]
    [SerializeField] private Vector2 windUpOffset = new Vector2(-0.5f, 2.5f);
    [SerializeField] private float windUpDuration = 0.5f;
    [Tooltip("Ease-in: starts slow, accelerates into the raised pose.")]
    [SerializeField] private AnimationCurve windUpCurve;

    // ── Swing Phase ───────────────────────────────────────────────────────────────

    [Header("Swing Phase")]
    [Tooltip("Bezier control point offset — the arc passes near here during the swing.")]
    [SerializeField] private Vector2 swingArcMidOffset = new Vector2(0.5f, 1f);

    [Tooltip("Arc endpoint — where the hammer slams down.")]
    [SerializeField] private Vector2 swingImpactOffset = new Vector2(1.5f, -1.5f);
    [SerializeField] private float swingDuration = 0.25f;
    [Tooltip("Sharp ease-in: explosive start, decelerates at impact.")]
    [SerializeField] private AnimationCurve swingCurve;

    // ── Impact Hold ───────────────────────────────────────────────────────────────

    [Header("Impact Hold")]
    [SerializeField] private float holdDuration = 0.3f;
    [Tooltip("Fires at the moment of impact.")]
    public UnityEvent OnImpact;

    // ── Recovery Phase ────────────────────────────────────────────────────────────

    [Header("Recovery Phase")]
    [Tooltip("Bezier control point for the return arc.")]
    [SerializeField] private Vector2 recoveryArcMidOffset = new Vector2(0.3f, 0.5f);
    [SerializeField] private float recoveryDuration = 0.8f;
    [Tooltip("Ease-out: starts moving, gently decelerates back to idle.")]
    [SerializeField] private AnimationCurve recoveryCurve;

    // ── Upper Arm ─────────────────────────────────────────────────────────────────

    [Header("Upper Arm")]
    [Tooltip("Fraction of the swing offset the shoulder moves. 0 = stays at idle. 0.4 = natural extension.")]
    [SerializeField] private float upperArmReachRatio = 0.4f;
    [Tooltip("Extra upward bow on the shoulder during wind-up, giving a natural pre-slam lift.")]
    [SerializeField] private float upperArmBowOffset = 0.3f;

    // ── Settings ──────────────────────────────────────────────────────────────────

    [Header("Settings")]
    [SerializeField] private float cooldown = 2f;
    [Tooltip("Read-only — shows attack state in the Inspector during Play Mode.")]
    [SerializeField] private bool isAttacking;
    public bool IsAttacking => isAttacking;

    // ── Private state ─────────────────────────────────────────────────────────────

    private Vector3 _idleHammerPos;
    private Vector3 _idleUpperPos;
    private bool    _idleSaved;

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

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

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>Start the attack. Ignored while already attacking.</summary>
    public void TriggerAttack()
    {
        if (!isAttacking) StartCoroutine(AttackRoutine());
    }

    /// <summary>Abort and snap arm back to idle (boss death, stagger, etc.).</summary>
    public void ResetArm()
    {
        StopAllCoroutines();
        isAttacking = false;
        if (hammerCollider != null) hammerCollider.SetActive(false);
        if (!_idleSaved) return;
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = _idleHammerPos;
        if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = _idleUpperPos;
    }

    // ── Attack sequence ───────────────────────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        float xDir  = transform.lossyScale.x >= 0f ? 1f : -1f;

        // Hammer target positions
        Vector3 windUp = _idleHammerPos + new Vector3(windUpOffset.x      * xDir, windUpOffset.y,      0f);
        Vector3 arcMid = _idleHammerPos + new Vector3(swingArcMidOffset.x * xDir, swingArcMidOffset.y, 0f);
        Vector3 impact = _idleHammerPos + new Vector3(swingImpactOffset.x * xDir, swingImpactOffset.y, 0f);
        Vector3 recMid = _idleHammerPos + new Vector3(recoveryArcMidOffset.x * xDir, recoveryArcMidOffset.y, 0f);

        // Upper arm positions: fraction of hammer offsets + bow lift during wind-up
        Vector3 upperWindUp = _idleUpperPos + new Vector3(
            windUpOffset.x      * xDir * upperArmReachRatio * 0.5f,
            windUpOffset.y      * upperArmReachRatio + upperArmBowOffset, 0f);
        Vector3 upperImpact = _idleUpperPos + new Vector3(
            swingImpactOffset.x * xDir * upperArmReachRatio,
            swingImpactOffset.y * upperArmReachRatio, 0f);

        // ── Phase 1: Wind-Up ─────────────────────────────────────────────────────
        float e = 0f;
        while (e < windUpDuration)
        {
            e += Time.deltaTime;
            float c = Eval(windUpCurve, e / windUpDuration);
            if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = Vector3.Lerp(_idleHammerPos, windUp,      c);
            if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = Vector3.Lerp(_idleUpperPos,  upperWindUp, c);
            yield return null;
        }
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = windUp;
        if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = upperWindUp;

        // ── Phase 2: Swing ───────────────────────────────────────────────────────
        Vector3 upperSwingCtrl = (upperWindUp + upperImpact) * 0.5f;
        e = 0f;
        while (e < swingDuration)
        {
            e += Time.deltaTime;
            float c = Eval(swingCurve, e / swingDuration);
            if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = QuadBezier(windUp,      arcMid,         impact,      c);
            if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = QuadBezier(upperWindUp, upperSwingCtrl, upperImpact, c);
            yield return null;
        }
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = impact;
        if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = upperImpact;

        // ── Phase 3: Impact Hold ─────────────────────────────────────────────────
        if (hammerCollider != null) hammerCollider.SetActive(true);
        OnImpact?.Invoke();
        yield return new WaitForSeconds(holdDuration);
        if (hammerCollider != null) hammerCollider.SetActive(false);

        // ── Phase 4: Recovery ────────────────────────────────────────────────────
        Vector3 upperRecCtrl = (upperImpact + _idleUpperPos) * 0.5f + Vector3.up * 0.3f;
        e = 0f;
        while (e < recoveryDuration)
        {
            e += Time.deltaTime;
            float c = Eval(recoveryCurve, e / recoveryDuration);
            if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = QuadBezier(impact,      recMid,       _idleHammerPos, c);
            if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = QuadBezier(upperImpact, upperRecCtrl, _idleUpperPos,  c);
            yield return null;
        }
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = _idleHammerPos;
        if (upperLeftIKTarget  != null) upperLeftIKTarget.position  = _idleUpperPos;

        isAttacking = false;
        yield return new WaitForSeconds(cooldown);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static float Eval(AnimationCurve c, float t) =>
        c != null ? c.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

    private static Vector3 QuadBezier(Vector3 a, Vector3 ctrl, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * ctrl + t * t * b;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────────

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

    private void EnsureDefaultCurves()
    {
        if (windUpCurve == null || windUpCurve.length == 0)
            windUpCurve   = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        if (swingCurve == null || swingCurve.length == 0)
            swingCurve    = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f), new Keyframe(1f, 1f, 0f, 0f));
        if (recoveryCurve == null || recoveryCurve.length == 0)
            recoveryCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }

    private void Reset()
    {
        windUpCurve   = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        swingCurve    = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f), new Keyframe(1f, 1f, 0f, 0f));
        recoveryCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }
}
