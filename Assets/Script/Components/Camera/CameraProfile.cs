using UnityEngine;

namespace Game.Components.CameraForesight
{
    /// <summary>
    /// Designer-authored tuning data for the camera foresight feature: directional
    /// look-ahead bias, momentum zoom, look-down on ledges, and the clearance kept
    /// from a room's bounding shape.
    ///
    /// Pure config. Read-only at runtime (no public setters), no behaviour, and no
    /// Cinemachine dependency - every consumer in this assembly is a function of
    /// this profile plus per-frame inputs, so the whole foresight layer is
    /// unit-testable without a live camera.
    ///
    /// Values here are starting points for designers to tune, not load-bearing
    /// constants. Author one asset per distinct camera feel; scenes whose vcam
    /// baseline orthographic size differs need their own asset so that
    /// <see cref="MinOrthographicSize"/> matches that scene's authored size.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraProfile", menuName = "Injection/Camera/Camera Profile")]
    public sealed class CameraProfile : ScriptableObject
    {
        [Header("Directional Bias")]
        [Tooltip("Maximum horizontal look-ahead offset, in world units, once a bias direction is established.")]
        [SerializeField] private float _biasMaxDistance = 5.5f;

        [Tooltip("Normalised speed factor (0-1, same domain as MovementComponent.SpeedFactor) the player must reach before a NEW bias direction may be established or flipped. Below it the established direction is simply held, so standing still or shuffling never jitters the camera.")]
        [Range(0f, 1f)]
        [SerializeField] private float _biasMinSpeedFactor = 0.5f;

        [Tooltip("Seconds of sustained opposite-direction movement required before the bias is allowed to flip. This is the anti-flicker hysteresis for rapid swing / wall-jump / dash direction changes.")]
        [SerializeField] private float _biasDwellTime = 0.4f;

        [Tooltip("Seconds to smoothly slide the camera between bias directions once the hysteresis allows a flip, covering the full BiasMaxDistance-to-BiasMaxDistance swing. CameraDirectionalBias itself outputs an instant all-or-nothing target by design (damping is the consumer's job) - this is that damping. 0 = instant snap, not recommended.")]
        [SerializeField] private float _biasSmoothTime = 0.35f;

        [Header("Momentum Zoom")]
        [Tooltip("Orthographic size at rest. Must match the authored baseline lens size of the vcam this profile drives, otherwise the camera pops on the first frame.")]
        [SerializeField] private float _minOrthographicSize = 8f;

        [Tooltip("Orthographic size at full momentum.")]
        [SerializeField] private float _maxOrthographicSize = 9.5f;

        [Tooltip("Maps speed factor (0-1 on X) to the lerp fraction from min toward max orthographic size (0-1 on Y). Output is clamped to 0-1, so a curve that overshoots can never zoom the camera outside the authored range.")]
        [SerializeField] private AnimationCurve _zoomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Normalised speed factor (0-1) the player must reach before momentum zoom engages at all. Below it, zoom recedes toward the baseline (min) size rather than tracking small speed changes - unlike bias, which holds its last direction at rest, standing still should let an earlier zoom-out recede rather than hold, so this gates toward 0 rather than toward the last value.")]
        [Range(0f, 1f)]
        [SerializeField] private float _zoomMinSpeedFactor = 0.5f;

        [Tooltip("Seconds of sustained above/below-gate movement required before momentum zoom engages or disengages. Anti-flicker hysteresis for speed hovering right at the gate threshold - same role BiasDwellTime plays for direction flips.")]
        [SerializeField] private float _zoomDwellTime = 0.3f;

        [Tooltip("Seconds to smoothly cover the full Min-to-Max orthographic size range once momentum calls for it. SpeedFactor can step instantly (e.g. Dash sets velocity directly, not via acceleration), so without this the zoom would snap in a single frame every time.")]
        [SerializeField] private float _zoomSmoothTime = 0.3f;

        [Header("Look Down")]
        [Tooltip("How far ahead of and below the player to probe for a drop, in world units.")]
        [SerializeField] private float _ledgeProbeDistance = 3f;

        [Tooltip("Minimum drop height below the player that counts as a hidden lower floor worth revealing, in world units. Shallower drops are ignored.")]
        [SerializeField] private float _ledgeMinDropHeight = 4f;

        [Tooltip("How far down the camera shifts when a qualifying ledge is detected, in world units. Magnitude only - the consumer applies the downward sign.")]
        [SerializeField] private float _lookDownOffset = 2.5f;

        [Tooltip("Seconds taken to pan down into the look-down offset.")]
        [SerializeField] private float _lookDownReactTime = 0.4f;

        [Tooltip("Seconds taken to return to the neutral offset after leaving the ledge. Usually slower than the react time so the camera does not snap back.")]
        [SerializeField] private float _lookDownRecoverTime = 0.6f;

        [Header("Confiner Safety")]
        [Tooltip("Clearance kept between the camera frustum and the room's bounding shape, in world units, before bias and zoom are pulled back.")]
        [SerializeField] private float _confinerMargin = 0.5f;

        public float BiasMaxDistance => _biasMaxDistance;
        public float BiasMinSpeedFactor => _biasMinSpeedFactor;
        public float BiasDwellTime => _biasDwellTime;
        public float BiasSmoothTime => _biasSmoothTime;

        public float MinOrthographicSize => _minOrthographicSize;
        public float MaxOrthographicSize => _maxOrthographicSize;
        public AnimationCurve ZoomCurve => _zoomCurve;
        public float ZoomMinSpeedFactor => _zoomMinSpeedFactor;
        public float ZoomDwellTime => _zoomDwellTime;
        public float ZoomSmoothTime => _zoomSmoothTime;

        public float LedgeProbeDistance => _ledgeProbeDistance;
        public float LedgeMinDropHeight => _ledgeMinDropHeight;
        public float LookDownOffset => _lookDownOffset;
        public float LookDownReactTime => _lookDownReactTime;
        public float LookDownRecoverTime => _lookDownRecoverTime;

        public float ConfinerMargin => _confinerMargin;

        private void OnValidate()
        {
            _biasMaxDistance = Mathf.Max(0f, _biasMaxDistance);
            _biasDwellTime = Mathf.Max(0f, _biasDwellTime);
            _biasSmoothTime = Mathf.Max(0f, _biasSmoothTime);

            // An inverted zoom range would zoom IN as the player speeds up, which reads
            // as a bug rather than a tuning choice - keep max at or above min.
            _minOrthographicSize = Mathf.Max(0.01f, _minOrthographicSize);
            _maxOrthographicSize = Mathf.Max(_minOrthographicSize, _maxOrthographicSize);
            _zoomDwellTime = Mathf.Max(0f, _zoomDwellTime);
            _zoomSmoothTime = Mathf.Max(0f, _zoomSmoothTime);

            _ledgeProbeDistance = Mathf.Max(0f, _ledgeProbeDistance);
            _ledgeMinDropHeight = Mathf.Max(0f, _ledgeMinDropHeight);
            _lookDownOffset = Mathf.Max(0f, _lookDownOffset);
            _lookDownReactTime = Mathf.Max(0f, _lookDownReactTime);
            _lookDownRecoverTime = Mathf.Max(0f, _lookDownRecoverTime);

            _confinerMargin = Mathf.Max(0f, _confinerMargin);
        }
    }
}
