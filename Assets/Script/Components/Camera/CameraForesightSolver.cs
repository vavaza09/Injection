using System;
using UnityEngine;

namespace Game.Components.CameraForesight
{
    /// <summary>
    /// Stateless foresight maths: momentum zoom and the look-down decision.
    ///
    /// Deliberately merged into one type rather than split per concern - both are
    /// single-expression pure functions of (profile, inputs) and a design review
    /// found separate files not worth the indirection.
    ///
    /// Holds no state whatsoever: the caller owns all smoothing and timing, so
    /// these results are instantaneous targets, not damped values.
    /// </summary>
    public static class CameraForesightSolver
    {
        /// <summary>
        /// Orthographic size for the given momentum.
        ///
        /// Contract: returns exactly <see cref="CameraProfile.MinOrthographicSize"/>
        /// at <paramref name="speedFactor"/> 0 and exactly
        /// <see cref="CameraProfile.MaxOrthographicSize"/> at 1 (given the default
        /// curve through (0,0) and (1,1)), is monotonically non-decreasing over that
        /// sweep for any monotonic curve, and never leaves the authored range even if
        /// the curve overshoots.
        /// </summary>
        /// <param name="speedFactor">
        /// Normalised speed, clamped to 0-1. Matches MovementComponent.SpeedFactor,
        /// which reports exactly 0 at rest - 0 is the baseline, never extrapolated below.
        /// </param>
        public static float ComputeZoomOrthoSize(CameraProfile profile, float speedFactor)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            float normalisedSpeed = Mathf.Clamp01(speedFactor);
            AnimationCurve curve = profile.ZoomCurve;

            // An unassigned or keyless curve degrades to identity (linear), which is
            // the documented default shape rather than a collapsed camera.
            float curveValue = curve != null && curve.length > 0
                ? curve.Evaluate(normalisedSpeed)
                : normalisedSpeed;

            curveValue = Mathf.Clamp01(curveValue);

            // Short-circuit the endpoints so they are bit-exact: a + (b - a) * 1 is not
            // guaranteed to reproduce b in floating point.
            if (curveValue <= 0f)
            {
                return profile.MinOrthographicSize;
            }

            if (curveValue >= 1f)
            {
                return profile.MaxOrthographicSize;
            }

            return Mathf.Lerp(profile.MinOrthographicSize, profile.MaxOrthographicSize, curveValue);
        }

        /// <summary>
        /// Instantaneous look-down decision: the downward offset magnitude the camera
        /// should be heading toward right now, in world units.
        ///
        /// Returns 0 unless a ledge is detected AND the drop is deep enough to be
        /// worth revealing. This is a hard decision, not a smoothed value - the caller
        /// applies <see cref="CameraProfile.LookDownReactTime"/> /
        /// <see cref="CameraProfile.LookDownRecoverTime"/> itself.
        /// </summary>
        /// <param name="dropHeight">
        /// Measured drop below the player, in world units, as a positive magnitude.
        /// </param>
        /// <returns>
        /// A positive magnitude; the caller applies the downward sign.
        /// </returns>
        public static float ComputeLookDownOffset(CameraProfile profile, bool ledgeDetected, float dropHeight)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (!ledgeDetected)
            {
                return 0f;
            }

            if (dropHeight < profile.LedgeMinDropHeight)
            {
                return 0f;
            }

            return profile.LookDownOffset;
        }
    }
}
