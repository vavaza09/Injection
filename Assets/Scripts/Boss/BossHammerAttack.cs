using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Hammer slam attack for a 2D sprite-based boss, driven by IK target Transforms.
// Detection and attack selection are handled by BossAttackManager — this script
// only runs the animation when TriggerAttack() is called.
//
// Arc path uses quadratic bezier interpolation so the arm swings naturally.
// Idle positions are captured once at Start; IK targets are not touched outside an attack.
public class BossHammerAttack : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Upper-arm IK root — idle position saved at Start.")]
    [SerializeField] private Transform upperLeftIKTarget;

    [Tooltip("Hammer IK effector target — drives the entire swing arc.")]
    [SerializeField] private Transform hammerLeftIKTarget;

    [Tooltip("'Hammer collider' GameObject — activated during impact hold only.")]
    [SerializeField] private GameObject hammerCollider;

    // ── Wind-Up Phase ─────────────────────────────────────────────────────

    [Header("Wind-Up Phase")]
    [SerializeField] private Vector2 windUpOffset = new Vector2(-0.5f, 2.5f);
    [SerializeField] private float windUpDuration = 0.5f;
    [Tooltip("Ease-in: starts slow, accelerates into the raised pose.")]
    [SerializeField] private AnimationCurve windUpCurve;

    // ── Swing Phase ───────────────────────────────────────────────────────

    [Header("Swing Phase")]
    [Tooltip("Bezier control point offset — the arc passes near here during the swing.")]
    [SerializeField] private Vector2 swingArcMidOffset = new Vector2(0.5f, 1f);

    [Tooltip("Arc endpoint — where the hammer slams down.")]
    [SerializeField] private Vector2 swingImpactOffset = new Vector2(1.5f, -1.5f);
    [SerializeField] private float swingDuration = 0.25f;
    [Tooltip("Sharp ease-in: explosive start, decelerates at impact.")]
    [SerializeField] private AnimationCurve swingCurve;

    // ── Impact Hold ───────────────────────────────────────────────────────

    [Header("Impact Hold")]
    [SerializeField] private float holdDuration = 0.3f;
    [Tooltip("Fires at the moment of impact.")]
    public UnityEvent OnImpact;

    // ── Recovery Phase ────────────────────────────────────────────────────

    [Header("Recovery Phase")]
    [Tooltip("Bezier control point for the return arc.")]
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

    // ── Public API ────────────────────────────────────────────────────────

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

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        float xDir    = transform.lossyScale.x >= 0f ? 1f : -1f;
        Vector3 windUp = _idleHammerPos + new Vector3(windUpOffset.x      * xDir, windUpOffset.y,      0f);
        Vector3 arcMid = _idleHammerPos + new Vector3(swingArcMidOffset.x * xDir, swingArcMidOffset.y, 0f);
        Vector3 impact = _idleHammerPos + new Vector3(swingImpactOffset.x * xDir, swingImpactOffset.y, 0f);
        Vector3 recMid = _idleHammerPos + new Vector3(recoveryArcMidOffset.x * xDir, recoveryArcMidOffset.y, 0f);

        // ── Wind-Up ──────────────────────────────────────────────────────
        yield return TweenWorld(hammerLeftIKTarget, _idleHammerPos, windUp, windUpDuration, windUpCurve);

        // ── Swing ────────────────────────────────────────────────────────
        yield return BezierTween(hammerLeftIKTarget, windUp, arcMid, impact, swingDuration, swingCurve);

        // ── Impact Hold ──────────────────────────────────────────────────
        if (hammerCollider != null) hammerCollider.SetActive(true);
        OnImpact?.Invoke();
        yield return new WaitForSeconds(holdDuration);
        if (hammerCollider != null) hammerCollider.SetActive(false);

        // ── Recovery ─────────────────────────────────────────────────────
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

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }

    // ── Setup ─────────────────────────────────────────────────────────────

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
