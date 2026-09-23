using UnityEngine;

namespace Game.Components.Glide
{
    /// <summary>Designer-tunable glide constants. Same shape as CameraProfile: a ScriptableObject
    /// living in this pure-logic asmdef so values can be tweaked in the Inspector with no code
    /// change and live-updated in Play Mode.
    ///
    /// IMPORTANT: the placeholder values below are calibrated against the LIVE Player prefab,
    /// read via SerializedObject (not the CLAUDE.md movement table, which documents
    /// MovementComponent's unused code defaults): gravity 10 (not 900), jumpSpeed 3.5,
    /// maxFall 14, risingGravityMultiplier 3. Because of a pre-existing, documented sign
    /// inconsistency in MovementComponent (see the project memory on this), the multiplier
    /// actually applied while FALLING is risingGravityMultiplier — so real fall accel is
    /// gravity * risingGravityMultiplier = 10 * 3 = 30 units/s^2 with no existing cap.
    /// glideCapBrakeStrength MUST exceed that (30) or the "cap" can never catch up to gravity's
    /// pull and the fall speed will keep creeping past it. Still tune in the Inspector against
    /// real play — this is a calibrated starting point, not a final feel.</summary>
    [CreateAssetMenu(menuName = "Injection/Glide Config", fileName = "GlideConfig")]
    public sealed class GlideConfig : ScriptableObject
    {
        [Tooltip("Fall speed (units/s, magnitude) glide eases the player toward while active. " +
                 "Calibrated against the live prefab's ~30 u/s^2 real fall accel and maxFall (14) " +
                 "— NOT the stale code-default 160 in CLAUDE.md's movement table.")]
        [SerializeField] private float glideMaxFallSpeed = 4f;

        [Tooltip("How fast (units/s^2) the fall-speed cap eases in when glide engages. Higher = " +
                 "snappier. This only smooths the engage transition — ending glide lifts the cap " +
                 "instantly so involuntary ends (damage, landing) feel responsive. Must exceed the " +
                 "live prefab's real fall acceleration (~30 u/s^2) or the cap can never catch up.")]
        [SerializeField] private float glideCapBrakeStrength = 60f;

        public float GlideMaxFallSpeed => glideMaxFallSpeed;
        public float GlideCapBrakeStrength => glideCapBrakeStrength;
    }
}
