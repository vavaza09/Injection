using System;
using System.Collections;
using UnityEngine;

public class BossHammerAttack : BossAttackBase
{
    private readonly GameObject _hitboxPrefab;
    private readonly Transform  _armRoot;    // Boss_Upper_L
    private readonly Transform  _hammerTip;  // Boss_Hammer(L) — hitbox spawn point
    private readonly float      _attackInterval;

    // Angles in local Z degrees
    private const float RaisedAngle  =  110f;  // arm swings way back overhead
    private const float SmashedAngle =  -60f;  // slams past resting position

    private const float WindUpTime  = 0.60f;   // slow raise — player sees it coming
    private const float PauseTime   = 0.20f;   // longer telegraph hold at the top
    private const float SlamTime    = 0.08f;   // nearly instant slam
    private const float RecoverTime = 0.45f;   // float back to rest

    private const float SmashYDrop = 0.15f;    // world-unit Y drop during smash phase

    private Quaternion       _restRotation;
    private float            _restY;
    private bool             _restCaptured;
    private BossIdleAnimation _idleAnim;

    public BossHammerAttack(BossBase boss, GameObject hitboxPrefab, Transform armRoot, Transform hammerTip, float attackInterval = 2f)
        : base(boss)
    {
        _hitboxPrefab   = hitboxPrefab;
        _armRoot        = armRoot;
        _hammerTip      = hammerTip;
        _attackInterval = attackInterval;
    }

    public override IEnumerator Execute()
    {
        // Cache once — boss MonoBehaviour won't change during a fight
        _idleAnim = boss.GetComponent<BossIdleAnimation>();

        // Capture rest pose once — before any swing touches the arm
        if (!_restCaptured && _armRoot != null)
        {
            _restRotation = _armRoot.localRotation;
            _restY        = _armRoot.localPosition.y;
            _restCaptured = true;
        }

        while (true)
        {
            // Idle bob plays freely during the interval between attacks
            yield return new WaitForSeconds(_attackInterval);
            yield return PerformSwing();
        }
    }

    public override void Reset()
    {
        // Restore bob in case attack was interrupted mid-swing
        _idleAnim?.ResumeLeftArm();
        if (_armRoot == null) return;
        _armRoot.localRotation = _restRotation;
        Vector3 p = _armRoot.localPosition;
        p.y = _restY;
        _armRoot.localPosition = p;
    }

    private IEnumerator PerformSwing()
    {
        // Freeze bob only for the duration of the actual swing
        _idleAnim?.PauseLeftArm();

        // Normalise rest Z to -180..180 for correct LerpAngle wrapping
        float restZ = _restRotation.eulerAngles.z;
        if (restZ > 180f) restZ -= 360f;

        // Phase 1 — Wind-up: ease-in (t²), slow charge → fast rise to overhead
        yield return TweenZ(_armRoot, restZ, RaisedAngle, WindUpTime, EaseIn);

        // Phase 2 — Telegraph pause at the top
        yield return new WaitForSeconds(PauseTime);

        // Phase 3 — Slam: rotation + Y drop together, ease-out (weight into floor)
        float smashStartY = _armRoot.localPosition.y;
        yield return TweenZAndY(_armRoot,
            RaisedAngle,  SmashedAngle,
            smashStartY,  smashStartY - SmashYDrop,
            SlamTime, EaseOut);

        // Phase 4 — Spawn hitbox at moment of impact
        if (_hitboxPrefab != null && _hammerTip != null)
            UnityEngine.Object.Instantiate(_hitboxPrefab, _hammerTip.position, Quaternion.identity);

        // Phase 5 — Recover: restore rotation and Y together, smooth ease-in-out
        float smashEndY = _armRoot.localPosition.y;
        yield return TweenZAndY(_armRoot,
            SmashedAngle, restZ,
            smashEndY,    _restY,
            RecoverTime, SmoothStep);

        // Arm is back at rest — let idle bob take over again
        _idleAnim?.ResumeLeftArm();
    }

    // ---------- tween helpers ----------

    private static IEnumerator TweenZ(Transform t, float fromZ, float toZ, float duration, Func<float, float> ease)
    {
        if (t == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float curved = ease(Mathf.Clamp01(elapsed / duration));
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromZ, toZ, curved));
            yield return null;
        }
        t.localRotation = Quaternion.Euler(0f, 0f, toZ);
    }

    private static IEnumerator TweenZAndY(Transform t, float fromZ, float toZ, float fromY, float toY, float duration, Func<float, float> ease)
    {
        if (t == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float curved = ease(Mathf.Clamp01(elapsed / duration));
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromZ, toZ, curved));
            Vector3 pos = t.localPosition;
            pos.y = Mathf.Lerp(fromY, toY, curved);
            t.localPosition = pos;
            yield return null;
        }
        t.localRotation = Quaternion.Euler(0f, 0f, toZ);
        Vector3 final = t.localPosition;
        final.y = toY;
        t.localPosition = final;
    }

    // ---------- easing curves ----------

    private static float EaseIn(float t)     => t * t;                      // slow → fast (charging power)
    private static float EaseOut(float t)    => 1f - (1f - t) * (1f - t);  // fast → slow (impact weight)
    private static float SmoothStep(float t) => t * t * (3f - 2f * t);     // ease-in-out (natural recovery)
}
