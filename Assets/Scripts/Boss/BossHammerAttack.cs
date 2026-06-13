using System;
using System.Collections;
using UnityEngine;

public class BossHammerAttack : BossAttackBase
{
    private readonly Transform _upperLJoint;   // Upper_L_Joint  — shoulder pivot
    private readonly Transform _lowerLJoint;   // Lower_L_Joint  — elbow pivot
    private readonly Transform _hammer;        // Boss_Hammer(L) — hammer pivot
    private readonly float     _upperThrustX;  // local X distance upper joint thrusts left
    private readonly float     _lowerThrustX;  // local X distance lower joint follows
    private readonly float     _attackRange;
    private readonly float     _cooldown;
    private readonly Action    _onImpact;

    // ── Z-rotation offsets FROM each bone's rest pose ────────────────────
    // Positive Z = CCW from front view. Flip sign if the arm swings the wrong way.
    private const float UpperWindUpZ  =  35f;  // shoulder raises into wind-up
    private const float HammerCockZ   =  60f;  // hammer rears back/up
    private const float HammerSmashZ  = -80f;  // hammer drives down hard

    // ── Per-stage hard-coded timings ──────────────────────────────────────
    private const float ThrustUpperTime   = 0.50f;  // Stage 1 : upper joint slides left
    private const float PauseAfterThrust1 = 0.15f;
    private const float RotateUpperTime   = 0.40f;  // Stage 2 : shoulder rotates up
    private const float PauseAfterRotate  = 0.15f;
    private const float ThrustLowerTime   = 0.45f;  // Stage 3 : lower joint follows left
    private const float PauseAfterThrust2 = 0.20f;
    private const float CockHammerTime    = 0.40f;  // Stage 4 : hammer rears back
    private const float PauseAfterCock    = 0.30f;
    private const float SmashTime         = 0.20f;  // Stage 5 : hammer slams down
    private const float HoldAtImpact      = 0.60f;  // Pause   : freeze at impact
    private const float RecoverTime       = 0.70f;  // Stage 6 : all bones return to rest

    // Captured rest state (positions + Z rotations)
    private Vector3 _upperRestPos;
    private Vector3 _lowerRestPos;
    private float   _upperRestZ;
    private float   _hammerRestZ;
    private bool    _restCaptured;

    private BossIdleAnimation _idleAnim;

    public BossHammerAttack(
        BossBase  boss,
        Transform upperLJoint,
        Transform lowerLJoint,
        Transform hammer       = null,
        float     upperThrustX = 1.5f,
        float     lowerThrustX = 1.0f,
        float     attackRange  = 5f,
        float     cooldown     = 10f,
        Action    onImpact     = null)
        : base(boss)
    {
        _upperLJoint  = upperLJoint;
        _lowerLJoint  = lowerLJoint;
        _hammer       = hammer;
        _upperThrustX = upperThrustX;
        _lowerThrustX = lowerThrustX;
        _attackRange  = attackRange;
        _cooldown     = cooldown;
        _onImpact     = onImpact;
    }

    // ── Main loop ─────────────────────────────────────────────────────────

    public override IEnumerator Execute()
    {
        _idleAnim = boss.GetComponent<BossIdleAnimation>();

        if (!_restCaptured)
        {
            _upperRestPos = _upperLJoint != null ? _upperLJoint.localPosition : Vector3.zero;
            _lowerRestPos = _lowerLJoint != null ? _lowerLJoint.localPosition : Vector3.zero;
            _upperRestZ   = ReadZ(_upperLJoint);
            _hammerRestZ  = ReadZ(_hammer);
            _restCaptured = true;
        }

        while (true)
        {
            yield return new WaitUntil(() => boss.IsPlayerInRange(_attackRange));
            yield return PerformSmash();
            yield return new WaitForSeconds(_cooldown);
        }
    }

    public override void Reset()
    {
        _idleAnim?.ResumeLeftArm();
        if (_upperLJoint != null) { _upperLJoint.localPosition = _upperRestPos; ApplyZ(_upperLJoint, _upperRestZ); }
        if (_lowerLJoint != null)   _lowerLJoint.localPosition = _lowerRestPos;
        ApplyZ(_hammer, _hammerRestZ);
    }

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator PerformSmash()
    {
        _idleAnim?.PauseLeftArm();

        // Pre-compute all target values
        Vector3 upperThrust   = _upperRestPos + new Vector3(-_upperThrustX, 0f, 0f);
        Vector3 lowerThrust   = _lowerRestPos + new Vector3(-_lowerThrustX, 0f, 0f);
        float   upperWindUpZ  = _upperRestZ  + UpperWindUpZ;
        float   hammerCockedZ = _hammerRestZ + HammerCockZ;
        float   hammerSmashZ  = _hammerRestZ + HammerSmashZ;

        // Stage 1 — Upper joint thrusts left: whole arm slides toward player
        yield return TweenPos(_upperLJoint, _upperRestPos, upperThrust, ThrustUpperTime, EaseOut);
        yield return new WaitForSeconds(PauseAfterThrust1);

        // Stage 2 — Upper joint rotates: shoulder raises, limb art orbits the pivot
        yield return TweenZ(_upperLJoint, _upperRestZ, upperWindUpZ, RotateUpperTime, EaseOut);
        yield return new WaitForSeconds(PauseAfterRotate);

        // Stage 3 — Lower joint thrusts left: elbow follows, bringing hammer into position
        yield return TweenPos(_lowerLJoint, _lowerRestPos, lowerThrust, ThrustLowerTime, EaseOut);
        yield return new WaitForSeconds(PauseAfterThrust2);

        // Stage 4 — Hammer cocks up (Boss_Hammer(L) rears back around its own pivot)
        yield return TweenZ(_hammer, _hammerRestZ, hammerCockedZ, CockHammerTime, EaseOut);
        yield return new WaitForSeconds(PauseAfterCock);

        // Stage 5 — Smash: hammer drives down hard (ease-in = accelerates into floor)
        yield return TweenZ(_hammer, hammerCockedZ, hammerSmashZ, SmashTime, EaseIn);

        // Impact — TODO: hook damage / camera shake / VFX / SFX here
        _onImpact?.Invoke();

        yield return new WaitForSeconds(HoldAtImpact);

        // Stage 6 — Recover: all bones return to rest simultaneously
        yield return RecoverAll(upperThrust, upperWindUpZ, lowerThrust, hammerSmashZ);

        _idleAnim?.ResumeLeftArm();
    }

    // Restore all 4 animated properties in one coroutine so nothing lags behind
    private IEnumerator RecoverAll(
        Vector3 fromUpperPos, float fromUpperZ,
        Vector3 fromLowerPos, float fromHammerZ)
    {
        float elapsed = 0f;
        while (elapsed < RecoverTime)
        {
            elapsed += Time.deltaTime;
            float c = SmoothStep(Mathf.Clamp01(elapsed / RecoverTime));

            if (_upperLJoint != null)
            {
                _upperLJoint.localPosition = Vector3.Lerp(fromUpperPos, _upperRestPos, c);
                ApplyZ(_upperLJoint, Mathf.LerpAngle(fromUpperZ, _upperRestZ, c));
            }
            if (_lowerLJoint != null)
                _lowerLJoint.localPosition = Vector3.Lerp(fromLowerPos, _lowerRestPos, c);
            ApplyZ(_hammer, Mathf.LerpAngle(fromHammerZ, _hammerRestZ, c));

            yield return null;
        }
        // Snap to exact rest to avoid float drift
        if (_upperLJoint != null) { _upperLJoint.localPosition = _upperRestPos; ApplyZ(_upperLJoint, _upperRestZ); }
        if (_lowerLJoint != null)   _lowerLJoint.localPosition = _lowerRestPos;
        ApplyZ(_hammer, _hammerRestZ);
    }

    // ── Tween helpers ─────────────────────────────────────────────────────

    private static IEnumerator TweenPos(
        Transform t, Vector3 from, Vector3 to,
        float duration, Func<float, float> ease)
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

    private static IEnumerator TweenZ(
        Transform t, float from, float to,
        float duration, Func<float, float> ease)
    {
        if (t == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyZ(t, Mathf.LerpAngle(from, to, ease(Mathf.Clamp01(elapsed / duration))));
            yield return null;
        }
        ApplyZ(t, to);
    }

    private static void ApplyZ(Transform t, float z)
    {
        if (t == null) return;
        Vector3 e = t.localEulerAngles;
        e.z = z;
        t.localEulerAngles = e;
    }

    private static float ReadZ(Transform t)   => t != null ? Normalize(t.localEulerAngles.z) : 0f;
    private static float Normalize(float a)   => a > 180f ? a - 360f : a;
    private static float EaseIn(float t)      => t * t;
    private static float EaseOut(float t)     => 1f - (1f - t) * (1f - t);
    private static float SmoothStep(float t)  => t * t * (3f - 2f * t);
}
