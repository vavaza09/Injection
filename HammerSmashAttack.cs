using System.Collections;
using UnityEngine;

/// <summary>
/// Left-arm hammer smash that both TRANSLATES (thrusts) and ROTATES (orbits)
/// each joint, matching the rig the user sketched:
///
///   Upper_L_Joint — translates LEFT (-X), then its arm rotates around it.
///   Lower_L_Joint — translates LEFT together with the upper joint.
///   Hammer (child of Lower_L_Joint via Boss_Hammer(L)) — rotates UP while the
///   arm is still translating, then rotates DOWN to smash the floor.
///
/// Each joint is treated as a pivot; the limb art is a child that orbits it,
/// so rotating the joint's transform swings the whole limb. Rotations are
/// around Z (camera-facing axis) so they read from the front view.
///
/// Fully staged: each action completes before the next begins, with pauses,
/// so the wind-up and smash are clearly readable. Pure code — no Animator.
/// </summary>
public class HammerSmashAttack : BossAttack
{
    [Header("Joints / pivots")]
    [Tooltip("Upper_L_Joint — shoulder pivot. Translates left, then rotates.")]
    [SerializeField] private Transform upperJoint;
    [Tooltip("Lower_L_Joint — elbow pivot. Translates with the upper joint.")]
    [SerializeField] private Transform lowerJoint;
    [Tooltip("Hammer pivot — Boss_Hammer(L) or its joint. Rotates up then smashes down.")]
    [SerializeField] private Transform hammer;

    [Header("Stage 1 — Upper joint thrusts left")]
    [Tooltip("Local-position offset for the upper joint (translate). -X = screen left.")]
    [SerializeField] private Vector3 upperTranslate = new Vector3(-1.5f, 0f, 0f);
    [SerializeField] private float upperTranslateDuration = 0.5f;
    [SerializeField] private float pauseAfterUpperTranslate = 0.15f;

    [Header("Stage 2 — Upper joint rotates (arm swings out)")]
    [Tooltip("Z-euler offset the upper arm rotates around its joint.")]
    [SerializeField] private Vector3 upperRotateEuler = new Vector3(0f, 0f, 35f);
    [SerializeField] private float upperRotateDuration = 0.4f;
    [SerializeField] private float pauseAfterUpperRotate = 0.15f;

    [Header("Stage 3 — Lower joint thrusts left (follows upper)")]
    [SerializeField] private Vector3 lowerTranslate = new Vector3(-1.0f, 0f, 0f);
    [SerializeField] private float lowerTranslateDuration = 0.45f;
    [SerializeField] private float pauseAfterLowerTranslate = 0.2f;

    [Header("Stage 4 — Hammer rotates UP (wind up) while translating")]
    [Tooltip("Z-euler the hammer rears up to.")]
    [SerializeField] private Vector3 hammerUpEuler = new Vector3(0f, 0f, 80f);
    [Tooltip("Extra translate applied to the lower joint as the hammer rears up.")]
    [SerializeField] private Vector3 hammerUpTranslate = new Vector3(-0.4f, 0.2f, 0f);
    [SerializeField] private float hammerUpDuration = 0.4f;
    [SerializeField] private float pauseAtTop = 0.35f;

    [Header("Stage 5 — Hammer SMASHES down")]
    [Tooltip("Z-euler the hammer slams down to.")]
    [SerializeField] private Vector3 hammerSmashEuler = new Vector3(0f, 0f, -95f);
    [SerializeField] private float hammerSmashDuration = 0.25f;
    [SerializeField] private float pauseAfterSmash = 0.5f;

    [Header("Recover")]
    [SerializeField] private float recoverDuration = 0.8f;

    // Cached rest state (position + rotation) for every animated transform.
    private Vector3 _upperPosRest, _lowerPosRest;
    private Quaternion _upperRotRest, _lowerRotRest, _hammerRotRest;
    private bool _cached;

    private void Awake() => CacheRest();

    private void CacheRest()
    {
        if (_cached) return;
        if (upperJoint) { _upperPosRest = upperJoint.localPosition; _upperRotRest = upperJoint.localRotation; }
        if (lowerJoint) { _lowerPosRest = lowerJoint.localPosition; _lowerRotRest = lowerJoint.localRotation; }
        if (hammer)     { _hammerRotRest = hammer.localRotation; }
        _cached = true;
    }

    protected override IEnumerator AttackRoutine()
    {
        CacheRest();

        Vector3 upperOut    = _upperPosRest + upperTranslate;
        Quaternion upperRot = _upperRotRest * Quaternion.Euler(upperRotateEuler);
        Vector3 lowerOut    = _lowerPosRest + lowerTranslate;
        Vector3 lowerTop    = lowerOut + hammerUpTranslate;
        Quaternion hammerUp = _hammerRotRest * Quaternion.Euler(hammerUpEuler);
        Quaternion hammerDn = _hammerRotRest * Quaternion.Euler(hammerSmashEuler);

        // Stage 1 — upper joint thrusts left.
        yield return MovePos(upperJoint, upperOut, upperTranslateDuration, EaseOut);
        yield return new WaitForSeconds(pauseAfterUpperTranslate);

        // Stage 2 — upper arm rotates around its joint.
        yield return MoveRot(upperJoint, upperRot, upperRotateDuration, EaseOut);
        yield return new WaitForSeconds(pauseAfterUpperRotate);

        // Stage 3 — lower joint thrusts left to follow the upper joint.
        yield return MovePos(lowerJoint, lowerOut, lowerTranslateDuration, EaseOut);
        yield return new WaitForSeconds(pauseAfterLowerTranslate);

        // Stage 4 — hammer rears UP while the lower joint keeps translating.
        yield return PosAndRot(lowerJoint, lowerTop, hammer, hammerUp, hammerUpDuration, EaseOut);
        yield return new WaitForSeconds(pauseAtTop);

        // Stage 5 — SMASH down.
        yield return MoveRot(hammer, hammerDn, hammerSmashDuration, EaseIn);

        // Impact — hook damage / VFX / screen shake here via OnImpact.
        RaiseImpact();
        yield return new WaitForSeconds(pauseAfterSmash);

        // Recover — translate AND rotate everything back to rest together.
        yield return RecoverAll(recoverDuration, EaseInOut);
    }

    // ---- Tween helpers ----

    private IEnumerator MovePos(Transform tr, Vector3 target, float dur, System.Func<float,float> ease)
    {
        if (!tr) yield break;
        Vector3 start = tr.localPosition;
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; tr.localPosition = Vector3.LerpUnclamped(start, target, ease(Mathf.Clamp01(t/dur))); yield return null; }
        tr.localPosition = target;
    }

    private IEnumerator MoveRot(Transform tr, Quaternion target, float dur, System.Func<float,float> ease)
    {
        if (!tr) yield break;
        Quaternion start = tr.localRotation;
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; tr.localRotation = Quaternion.Slerp(start, target, ease(Mathf.Clamp01(t/dur))); yield return null; }
        tr.localRotation = target;
    }

    // Translate one transform and rotate another over the same window.
    private IEnumerator PosAndRot(Transform posTr, Vector3 posTarget, Transform rotTr, Quaternion rotTarget,
                                  float dur, System.Func<float,float> ease)
    {
        Vector3 posStart = posTr ? posTr.localPosition : Vector3.zero;
        Quaternion rotStart = rotTr ? rotTr.localRotation : Quaternion.identity;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = ease(Mathf.Clamp01(t/dur));
            if (posTr) posTr.localPosition = Vector3.LerpUnclamped(posStart, posTarget, k);
            if (rotTr) rotTr.localRotation = Quaternion.Slerp(rotStart, rotTarget, k);
            yield return null;
        }
        if (posTr) posTr.localPosition = posTarget;
        if (rotTr) rotTr.localRotation = rotTarget;
    }

    // Return all joints' position + rotation to rest together.
    private IEnumerator RecoverAll(float dur, System.Func<float,float> ease)
    {
        Vector3 uP = upperJoint ? upperJoint.localPosition : Vector3.zero;
        Quaternion uR = upperJoint ? upperJoint.localRotation : Quaternion.identity;
        Vector3 lP = lowerJoint ? lowerJoint.localPosition : Vector3.zero;
        Quaternion lR = lowerJoint ? lowerJoint.localRotation : Quaternion.identity;
        Quaternion hR = hammer ? hammer.localRotation : Quaternion.identity;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = ease(Mathf.Clamp01(t/dur));
            if (upperJoint) { upperJoint.localPosition = Vector3.LerpUnclamped(uP, _upperPosRest, k); upperJoint.localRotation = Quaternion.Slerp(uR, _upperRotRest, k); }
            if (lowerJoint) { lowerJoint.localPosition = Vector3.LerpUnclamped(lP, _lowerPosRest, k); lowerJoint.localRotation = Quaternion.Slerp(lR, _lowerRotRest, k); }
            if (hammer)     { hammer.localRotation = Quaternion.Slerp(hR, _hammerRotRest, k); }
            yield return null;
        }
        if (upperJoint) { upperJoint.localPosition = _upperPosRest; upperJoint.localRotation = _upperRotRest; }
        if (lowerJoint) { lowerJoint.localPosition = _lowerPosRest; lowerJoint.localRotation = _lowerRotRest; }
        if (hammer)     { hammer.localRotation = _hammerRotRest; }
    }

    // Easing helpers.
    private static float EaseIn(float x)    => x * x;
    private static float EaseOut(float x)   => 1f - (1f - x) * (1f - x);
    private static float EaseInOut(float x) => x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
}
