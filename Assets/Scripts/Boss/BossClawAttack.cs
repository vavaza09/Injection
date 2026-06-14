using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Claw strike for a 2D sprite-based boss.
// BOTH the shoulder (Boss_Upper_R) AND the end-effector (Boss_Arm_Right_LimbSolver2D_Target)
// move simultaneously so the entire arm extends to reach the player.
// The BoxCollider2D (clawAttackZone) is the reach limit — no separate maxReachDistance.
//
// Phases:
//   1. Anticipation : Both IK targets pull BACK away from player direction.
//   2. Strike       : Upper arm extends toward player; effector hits exact player position.
//   3. Hold         : Arm stays at impact; damage collider active; OnClawImpact fires.
//   4. Retract      : Both IK targets arc back to idle simultaneously.
//
// Attach to the boss root. Call TriggerClawAttack() from BossAttackManager.
// CanAttack returns true when player is inside clawAttackZone and the claw is free.
public class BossClawAttack : MonoBehaviour
{
    // ── IK Targets ────────────────────────────────────────────────────────

    [Header("IK Targets")]
    [Tooltip("Boss_Upper_R — shoulder bone. Moves toward the player to extend the arm's reach.")]
    [SerializeField] private Transform upperRightIKTarget;

    [Tooltip("Boss_Arm_Right_LimbSolver2D_Target — end effector. Moves to the player's exact position.")]
    [SerializeField] private Transform clawRightIKTarget;

    // ── Colliders ─────────────────────────────────────────────────────────

    [Header("Colliders")]
    [Tooltip("'Claw collider' GO — moved to the strike position and activated during hold only.")]
    [SerializeField] private GameObject clawCollider;

    [Tooltip("BoxCollider2D trigger — the green box in the scene. THIS is the reach limit.\n" +
             "CanAttack is false when the player is outside this collider.")]
    [SerializeField] private Collider2D clawAttackZone;

    // ── Player ────────────────────────────────────────────────────────────

    [Header("Player")]
    [Tooltip("Auto-found by 'Player' tag on Start if left empty.")]
    [SerializeField] private Transform playerTransform;

    // ── Anticipation Phase ────────────────────────────────────────────────

    [Header("Anticipation Phase  (~0.3 s)")]
    [Tooltip("How far the claw end-effector pulls back, away from the player direction.")]
    [SerializeField] private float anticipationDistance = 1.5f;

    [Tooltip("Extra upward component on the pull-back so the arm lifts before striking.")]
    [SerializeField] private float anticipationLift = 1.0f;

    [SerializeField] private float anticipationDuration = 0.3f;
    [Tooltip("Ease-in: slow start, builds into the pulled-back pose.")]
    [SerializeField] private AnimationCurve anticipationCurve;

    // ── Strike Phase ──────────────────────────────────────────────────────

    [Header("Strike Phase  (~0.15 s)")]
    [Tooltip("Fraction of the boss→player vector the shoulder moves during the strike.\n" +
             "0 = shoulder stays at idle. 1 = shoulder moves all the way to the player.")]
    [SerializeField] private float upperArmReachRatio = 0.4f;

    [Tooltip("Upward offset applied to the shoulder's strike position for a natural arm bow.")]
    [SerializeField] private float upperArmBowOffset = 0.3f;

    [SerializeField] private float strikeDuration = 0.15f;
    [Tooltip("Sharp ease-in: explosive lunge toward the player.")]
    [SerializeField] private AnimationCurve strikeCurve;

    // ── Hold Phase ────────────────────────────────────────────────────────

    [Header("Hold Phase  (~0.2 s)")]
    [SerializeField] private float holdDuration = 0.2f;
    [Tooltip("Fires at the moment of impact. Wire up SFX, screenshake, damage here.")]
    public UnityEvent OnClawImpact;

    // ── Retract Phase ─────────────────────────────────────────────────────

    [Header("Retract Phase  (~0.6 s)")]
    [SerializeField] private float retractDuration = 0.6f;
    [Tooltip("Ease-out: quick start, gentle deceleration back into idle.")]
    [SerializeField] private AnimationCurve retractCurve;

    // ── Debug ─────────────────────────────────────────────────────────────

    [Header("Debug — Read Only")]
    [SerializeField] private bool isAttacking;
    public bool IsAttacking => isAttacking;

    /// <summary>True when the player is inside the attack zone collider and the claw is free.</summary>
    public bool CanAttack
    {
        get
        {
            if (isAttacking || playerTransform == null) return false;
            if (clawAttackZone != null)
                return clawAttackZone.OverlapPoint(playerTransform.position);
            return false;
        }
    }

    // ── Private state ─────────────────────────────────────────────────────

    private Vector3 _idleClawPos;
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
        if (clawCollider != null) clawCollider.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Begin the claw strike. Ignored while already attacking.</summary>
    public void TriggerClawAttack()
    {
        if (!isAttacking) StartCoroutine(ClawRoutine());
    }

    /// <summary>Abort immediately and snap both IK targets back to idle.</summary>
    public void ResetClaw()
    {
        StopAllCoroutines();
        isAttacking = false;
        if (clawCollider != null) clawCollider.SetActive(false);
        if (!_idleSaved) return;
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = _idleClawPos;
        if (upperRightIKTarget != null) upperRightIKTarget.position = _idleUpperPos;
    }

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator ClawRoutine()
    {
        isAttacking = true;

        Vector2 toPlayer2D = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Vector2 pullBack   = -toPlayer2D;

        // ── Phase 1: Anticipation ────────────────────────────────────────
        // End-effector pulls back away from player; shoulder pulls back at 30% of that.
        Vector3 clawAntPos  = _idleClawPos
                            + (Vector3)(pullBack * anticipationDistance)
                            + Vector3.up * anticipationLift;
        Vector3 upperAntPos = _idleUpperPos + (Vector3)(pullBack * anticipationDistance * 0.3f);

        float e = 0f;
        while (e < anticipationDuration)
        {
            e += Time.deltaTime;
            float c = Eval(anticipationCurve, e / anticipationDuration);
            if (clawRightIKTarget  != null) clawRightIKTarget.position  = Vector3.Lerp(_idleClawPos, clawAntPos,  c);
            if (upperRightIKTarget != null) upperRightIKTarget.position = Vector3.Lerp(_idleUpperPos, upperAntPos, c);
            yield return null;
        }
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = clawAntPos;
        if (upperRightIKTarget != null) upperRightIKTarget.position = upperAntPos;

        // ── Phase 2: Strike ──────────────────────────────────────────────
        // End-effector hits exact player position; shoulder extends by upperArmReachRatio.
        Vector3 strikeTarget = playerTransform.position;
        strikeTarget.z = _idleClawPos.z;

        Vector2 toStrike     = (Vector2)playerTransform.position - (Vector2)transform.position;
        Vector3 upperStrike  = _idleUpperPos
                             + (Vector3)((Vector3)toStrike * upperArmReachRatio)
                             + Vector3.up * upperArmBowOffset;
        upperStrike.z = _idleUpperPos.z;

        Vector3 strikeCtrl = (clawAntPos  + strikeTarget) * 0.5f + Vector3.up * 0.5f;
        Vector3 upperCtrl  = (upperAntPos + upperStrike)  * 0.5f;

        e = 0f;
        while (e < strikeDuration)
        {
            e += Time.deltaTime;
            float c = Eval(strikeCurve, e / strikeDuration);
            if (clawRightIKTarget  != null) clawRightIKTarget.position  = QuadBezier(clawAntPos,  strikeCtrl, strikeTarget, c);
            if (upperRightIKTarget != null) upperRightIKTarget.position = QuadBezier(upperAntPos, upperCtrl,  upperStrike,  c);
            yield return null;
        }
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = strikeTarget;
        if (upperRightIKTarget != null) upperRightIKTarget.position = upperStrike;

        // Move damage collider to exact strike position and activate.
        if (clawCollider != null)
        {
            clawCollider.transform.position = new Vector3(strikeTarget.x, strikeTarget.y,
                                                          clawCollider.transform.position.z);
            clawCollider.SetActive(true);
        }

        // ── Phase 3: Hold ────────────────────────────────────────────────
        OnClawImpact?.Invoke();
        yield return new WaitForSeconds(holdDuration);
        if (clawCollider != null) clawCollider.SetActive(false);

        // ── Phase 4: Retract ─────────────────────────────────────────────
        // Both IK targets arc back to saved idle positions simultaneously.
        Vector3 retractCtrl      = (strikeTarget + _idleClawPos)  * 0.5f + Vector3.up * 0.4f;
        Vector3 upperRetractCtrl = (upperStrike  + _idleUpperPos) * 0.5f;

        e = 0f;
        while (e < retractDuration)
        {
            e += Time.deltaTime;
            float c = Eval(retractCurve, e / retractDuration);
            if (clawRightIKTarget  != null) clawRightIKTarget.position  = QuadBezier(strikeTarget, retractCtrl,      _idleClawPos,  c);
            if (upperRightIKTarget != null) upperRightIKTarget.position = QuadBezier(upperStrike,  upperRetractCtrl, _idleUpperPos, c);
            yield return null;
        }
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = _idleClawPos;
        if (upperRightIKTarget != null) upperRightIKTarget.position = _idleUpperPos;

        isAttacking = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static float Eval(AnimationCurve c, float t) =>
        c != null ? c.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

    private static Vector3 QuadBezier(Vector3 a, Vector3 ctrl, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * ctrl + t * t * b;
    }

    // ── Setup helpers ─────────────────────────────────────────────────────

    private void SaveIdlePositions()
    {
        if (_idleSaved) return;
        _idleClawPos  = clawRightIKTarget  != null ? clawRightIKTarget.position  : Vector3.zero;
        _idleUpperPos = upperRightIKTarget != null ? upperRightIKTarget.position : Vector3.zero;
        _idleSaved    = true;
    }

    private void ResolveReferences()
    {
        if (playerTransform == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTransform = go.transform;
        }

        if (upperRightIKTarget == null)
            upperRightIKTarget = FindDeep("Boss_Upper_R");

        if (clawRightIKTarget == null)
            clawRightIKTarget = FindDeep("Boss_Arm_Right_LimbSolver2D_Target")
                             ?? FindScene("Boss_Arm_Right_LimbSolver2D_Target");

        if (clawCollider == null)
        {
            var t = FindDeep("Claw collider") ?? FindScene("Claw collider");
            if (t != null) clawCollider = t.gameObject;
        }

        if (upperRightIKTarget == null) Debug.LogWarning("[BossClawAttack] 'Boss_Upper_R' not found — assign in Inspector.");
        if (clawRightIKTarget  == null) Debug.LogWarning("[BossClawAttack] 'Boss_Arm_Right_LimbSolver2D_Target' not found — assign in Inspector.");
        if (clawCollider       == null) Debug.LogWarning("[BossClawAttack] 'Claw collider' not found — assign in Inspector.");
        if (clawAttackZone     == null) Debug.LogWarning("[BossClawAttack] 'Claw Attack Zone' collider not assigned — claw will never attack.");
        if (playerTransform    == null) Debug.LogWarning("[BossClawAttack] Player not found — tag the player 'Player'.");
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
        if (anticipationCurve == null || anticipationCurve.length == 0)
            anticipationCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        if (strikeCurve == null || strikeCurve.length == 0)
            strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 5f, 5f), new Keyframe(1f, 1f, 0f, 0f));
        if (retractCurve == null || retractCurve.length == 0)
            retractCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }

    private void Reset()
    {
        anticipationCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        strikeCurve       = new AnimationCurve(new Keyframe(0f, 0f, 5f, 5f), new Keyframe(1f, 1f, 0f, 0f));
        retractCurve      = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }
}
