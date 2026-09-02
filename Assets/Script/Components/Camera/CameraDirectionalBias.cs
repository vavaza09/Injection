using System;
using UnityEngine;

namespace Game.Components.CameraForesight
{
    /// <summary>
    /// Horizontal look-ahead bias with flip hysteresis.
    ///
    /// Unlike the rest of this assembly this type is stateful on purpose: the
    /// anti-flicker guarantee needs memory between frames. Rapid direction changes
    /// (swing, wall-jump, dash cancel) must not whip the camera back and forth, so a
    /// flip only happens after the player has committed to the opposite direction
    /// for <see cref="CameraProfile.BiasDwellTime"/> seconds.
    ///
    /// Behaviour:
    /// - The bias magnitude is all-or-nothing (<see cref="CameraProfile.BiasMaxDistance"/>);
    ///   the speed factor gates whether the DIRECTION may change, it does not scale
    ///   the distance. Damping the offset is the consumer's job.
    /// - Going idle holds the last direction rather than recentring, so stopping at
    ///   the edge of a drop keeps showing what is ahead.
    /// - Nothing is emitted until a first qualifying sample establishes a direction,
    ///   so a fresh (or just-<see cref="Reset"/>) instance returns zero.
    /// </summary>
    public sealed class CameraDirectionalBias
    {
        // Below this the horizontal component is treated as no direction at all, so
        // near-vertical motion (wall-jump, fast fall) cannot establish or flip a bias.
        private const float HORIZONTAL_EPSILON = 0.01f;

        private int _biasSign;
        private float _flipDwellTimer;

        /// <summary>
        /// Advances the hysteresis state and returns the bias offset to apply, in
        /// world units. Call once per frame.
        /// </summary>
        /// <param name="velocity">
        /// Player velocity. Only the horizontal sign is used - the magnitude does not
        /// scale the offset.
        /// </param>
        /// <param name="speedFactor">
        /// Normalised speed (0-1) from MovementComponent.SpeedFactor, compared against
        /// <see cref="CameraProfile.BiasMinSpeedFactor"/>. Passed in separately rather
        /// than derived from <paramref name="velocity"/> because normalising raw
        /// world-units-per-second here would mean duplicating the character's max
        /// speed into the profile, where it could silently drift out of sync.
        /// </param>
        /// <returns>
        /// A horizontal offset (Y is always 0) of magnitude
        /// <see cref="CameraProfile.BiasMaxDistance"/>, or zero while no direction is
        /// established.
        /// </returns>
        public Vector2 Update(CameraProfile profile, Vector2 velocity, float speedFactor, float deltaTime)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            int observedSign = 0;
            if (speedFactor >= profile.BiasMinSpeedFactor && Mathf.Abs(velocity.x) > HORIZONTAL_EPSILON)
            {
                observedSign = velocity.x > 0f ? 1 : -1;
            }

            if (observedSign == 0)
            {
                // Not enough committed horizontal motion to justify any change: hold the
                // established direction and forget a partial flip streak, so a flip needs
                // sustained fast movement rather than an accumulation of slow drift.
                _flipDwellTimer = 0f;
            }
            else if (_biasSign == 0)
            {
                // Nothing established yet (fresh instance, or just after Reset). Adopt
                // immediately - the dwell time governs flips, not the first commitment.
                _biasSign = observedSign;
                _flipDwellTimer = 0f;
            }
            else if (observedSign == _biasSign)
            {
                _flipDwellTimer = 0f;
            }
            else
            {
                _flipDwellTimer += Mathf.Max(0f, deltaTime);
                if (_flipDwellTimer >= profile.BiasDwellTime)
                {
                    _biasSign = observedSign;
                    _flipDwellTimer = 0f;
                }
            }

            return new Vector2(_biasSign * profile.BiasMaxDistance, 0f);
        }

        /// <summary>
        /// Hard, complete state wipe - no smoothing, no residual direction, no dwell
        /// debt. The next <see cref="Update"/> returns zero unless that same call
        /// carries a qualifying sample.
        ///
        /// Consumers must call this whenever their camera goes from non-live to live:
        /// Cinemachine's round-robin standby update leaves a non-live vcam's state
        /// arbitrarily stale, and resuming from it would snap the camera.
        /// </summary>
        public void Reset()
        {
            _biasSign = 0;
            _flipDwellTimer = 0f;
        }
    }
}
