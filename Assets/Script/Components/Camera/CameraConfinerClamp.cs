using UnityEngine;

namespace Game.Components.CameraForesight
{
    /// <summary>
    /// Independent safety net that keeps a foresight-adjusted camera inside its
    /// room. Stateless, and takes plain data only - no Cinemachine types.
    ///
    /// This is NOT a wrapper around CinemachineConfiner2D. That component caches its
    /// confinement solution against a frustum size baked once, and does not rebake
    /// when orthographic size changes at runtime, so it cannot be trusted to contain
    /// a camera that zooms dynamically. Both methods here recompute from the frustum
    /// actually in effect this frame.
    ///
    /// Both methods degrade smoothly (proportional scale-down) rather than switching
    /// the effect off, and neither ever amplifies what was requested.
    /// </summary>
    public static class CameraConfinerClamp
    {
        private const float EPSILON = 0.0001f;

        /// <summary>
        /// Scales a requested bias offset down - uniformly, so its direction is
        /// preserved exactly - until the frustum placed at
        /// <paramref name="cameraCenter"/> + bias fits inside
        /// <paramref name="roomBounds"/> shrunk inward by <paramref name="margin"/>.
        /// </summary>
        /// <param name="halfFrustumWidth">Half frustum width, already including any zoom in effect.</param>
        /// <param name="halfFrustumHeight">Half frustum height, already including any zoom in effect.</param>
        /// <returns>
        /// The requested bias scaled by 0-1, or <see cref="Vector2.zero"/> when the
        /// camera is already outside the safe region (including when the room is too
        /// small to contain the frustum at all), where no bias can improve matters.
        /// </returns>
        public static Vector2 ClampBias(
            Vector2 requestedBias,
            Vector2 cameraCenter,
            float halfFrustumWidth,
            float halfFrustumHeight,
            Bounds roomBounds,
            float margin)
        {
            float safeMargin = Mathf.Max(0f, margin);
            float halfWidth = Mathf.Max(0f, halfFrustumWidth);
            float halfHeight = Mathf.Max(0f, halfFrustumHeight);

            // The region the camera CENTRE may occupy: the room, inset by the margin and
            // then by the frustum half-extents.
            float safeMinX = roomBounds.min.x + safeMargin + halfWidth;
            float safeMaxX = roomBounds.max.x - safeMargin - halfWidth;
            float safeMinY = roomBounds.min.y + safeMargin + halfHeight;
            float safeMaxY = roomBounds.max.y - safeMargin - halfHeight;

            // Already out (or the safe region is empty because the room is smaller than
            // the frustum, in which case no centre satisfies it and this test fails too).
            // Suppress entirely rather than risk pushing further out.
            if (cameraCenter.x < safeMinX || cameraCenter.x > safeMaxX ||
                cameraCenter.y < safeMinY || cameraCenter.y > safeMaxY)
            {
                return Vector2.zero;
            }

            // One uniform scale from the tighter axis. Scaling each axis independently
            // would skew the offset's direction instead of merely shortening it.
            float scale = Mathf.Min(
                AxisScale(requestedBias.x, cameraCenter.x, safeMinX, safeMaxX),
                AxisScale(requestedBias.y, cameraCenter.y, safeMinY, safeMaxY));

            return requestedBias * Mathf.Clamp01(scale);
        }

        /// <summary>
        /// Clamps a requested orthographic size down until the resulting frustum at
        /// <paramref name="cameraCenter"/> fits inside <paramref name="roomBounds"/>
        /// shrunk inward by <paramref name="margin"/>.
        /// </summary>
        /// <param name="aspectRatio">Frustum width / height.</param>
        /// <returns>
        /// A size in the range [<paramref name="minOrthoSize"/>,
        /// <paramref name="requestedOrthoSize"/>]. Never below
        /// <paramref name="minOrthoSize"/>: if even the baseline size cannot fit, slight
        /// overscan is a far better failure than a collapsed camera.
        /// </returns>
        public static float ClampMaxOrthoSize(
            float requestedOrthoSize,
            float minOrthoSize,
            Vector2 cameraCenter,
            float aspectRatio,
            Bounds roomBounds,
            float margin)
        {
            float safeMargin = Mathf.Max(0f, margin);
            float safeAspect = Mathf.Max(EPSILON, aspectRatio);
            float floorSize = Mathf.Max(0f, minOrthoSize);

            // Largest half-height that still clears the top and bottom edges.
            float allowedHalfHeight = Mathf.Min(
                cameraCenter.y - (roomBounds.min.y + safeMargin),
                (roomBounds.max.y - safeMargin) - cameraCenter.y);

            // Largest half-width that still clears the left and right edges. Orthographic
            // size is a half-HEIGHT, so convert through the aspect ratio.
            float allowedHalfWidth = Mathf.Min(
                cameraCenter.x - (roomBounds.min.x + safeMargin),
                (roomBounds.max.x - safeMargin) - cameraCenter.x);

            float allowed = Mathf.Min(allowedHalfHeight, allowedHalfWidth / safeAspect);

            if (allowed < floorSize)
            {
                allowed = floorSize;
            }

            return Mathf.Max(floorSize, Mathf.Min(requestedOrthoSize, allowed));
        }

        /// <summary>
        /// Largest fraction of <paramref name="bias"/> that keeps
        /// <paramref name="center"/> + bias within the safe span on one axis. May
        /// exceed 1 when the axis does not constrain the request at all; the caller
        /// clamps.
        /// </summary>
        private static float AxisScale(float bias, float center, float safeMin, float safeMax)
        {
            if (bias > EPSILON)
            {
                return (safeMax - center) / bias;
            }

            if (bias < -EPSILON)
            {
                return (center - safeMin) / -bias;
            }

            return 1f;
        }
    }
}
