using System.Collections.Generic;
using Game.Components.CameraForesight;
using Game.Components.Movement;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using Core.Logging;

/// <summary>
/// Wires the pure-logic camera foresight layer (<see cref="CameraProfile"/>,
/// <see cref="CameraForesightSolver"/>, <see cref="CameraDirectionalBias"/>,
/// <see cref="CameraConfinerClamp"/>) into the live Cinemachine pipeline.
///
/// Lives in the global namespace and loose in Assembly-CSharp, matching every other script in
/// Assets/Script/Camera_script/ — it needs Player-side types (MovementComponent) and the
/// VContainer logger, and only *consumes* the Game.Components.CameraForesight asmdef.
///
/// Composition with the other scripts writing the same vcam:
/// - Position: contributes only to <c>CameraState.PositionCorrection</c>, never RawPosition and
///   never <c>CinemachinePositionComposer.TargetOffset</c> (that field is owned by
///   CinemachineAimLean and RoomLockTrigger's pan proxy — writing it here would fight both).
/// - Zoom: see the long note on <see cref="ApplyZoom"/>. Short version: this class contributes a
///   momentum DELTA on top of whatever absolute size is already in the per-frame state, so
///   RoomLockTrigger's boss zoom-to-10 and CameraZoomTrigger's zone zoom-to-6 keep working.
///
/// Room containment does NOT rely on extension ordering relative to CinemachineConfiner2D (that
/// order is genuinely inconsistent between a fresh load and a post-script-reload Editor state).
/// CameraConfinerClamp is an independent safety net fed by the confiner's BoundingShape2D bounds,
/// read as a black box. The execution order below is a debugging nicety, not load-bearing.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class CameraForesightExtension : CinemachineExtension
{
    // Live instances, for the static Suspend/Resume broadcast. Small and changes rarely (one per
    // room vcam), mirroring CinemachineAimLean's own fixed-array reasoning.
    private static readonly List<CameraForesightExtension> LiveInstances = new List<CameraForesightExtension>();

    private const float HORIZONTAL_EPSILON = 0.01f;

    // "Found nothing" past this depth means "no floor within reach", which for a sidescroller is
    // a pit — the strongest case for revealing what is below, so it reports as a qualifying drop
    // of exactly this depth rather than as "no ledge".
    private const float LEDGE_PROBE_DEPTH_MULTIPLIER = 2f;

    [Header("Foresight")]
    [Tooltip("Designer-authored tuning. Required — the component disables itself without one.")]
    [SerializeField] private CameraProfile _profile;

    [Tooltip("Layers the ledge probe treats as floor. Left empty, falls back to Ground + Platform, matching MovementComponent's own ground cast filter.")]
    [SerializeField] private LayerMask _groundMask;

    private readonly CameraDirectionalBias _bias = new CameraDirectionalBias();
    private Vector2 _biasCurrent;
    private readonly CameraZoomGate _zoomGate = new CameraZoomGate();
    private float _zoomDeltaCurrent;
    private readonly RaycastHit2D[] _ledgeHits = new RaycastHit2D[4];
    private ContactFilter2D _ledgeFilter;

    private CinemachineConfiner2D _confiner;

    private Bounds _cachedRoomBounds;
    private bool _hasCachedRoomBounds;

    private Transform _resolvedFollow;
    private MovementComponent _movement;
    private Rigidbody2D _followBody;

    private bool _ledgeDetected;
    private float _ledgeDropHeight;
    private int _probeSign = 1;

    private float _lookDownCurrent;

    private bool _suspended;
    private bool _profileMissing;
    private bool _confinerWarningLogged;

    private Core.Logging.ILogger _logger;

    /// <summary>
    /// Injected post-hoc by <c>SceneLifetimeScope.InjectAll</c>, same as RoomSpawner /
    /// SavePointTrigger — this component is scene-placed and nothing resolves it.
    /// </summary>
    [Inject]
    public void Construct(LoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<CameraForesightExtension>();
    }

    /// <summary>
    /// Suspends foresight on every live instance: while suspended the pipeline callback passes
    /// the camera state through completely untouched.
    ///
    /// Broadcast rather than targeted because RoomLockTrigger's serialized roomVcam can resolve
    /// to the wrong camera in the two scenes with more than one; a broadcast reaches whichever
    /// vcam is actually live. Idempotent (a flag, not a counter) because RestoreCamera can
    /// legitimately run twice — once from OnPlayerDied, once later via OpenRoom/ReleaseCamera.
    /// </summary>
    public static void SuspendAll()
    {
        for (int index = 0; index < LiveInstances.Count; index++)
        {
            LiveInstances[index].SetSuspended(true);
        }
    }

    /// <summary>Resumes foresight on every live instance. Idempotent; see <see cref="SuspendAll"/>.</summary>
    public static void ResumeAll()
    {
        for (int index = 0; index < LiveInstances.Count; index++)
        {
            LiveInstances[index].SetSuspended(false);
        }
    }

    protected override void Awake()
    {
        // Mandatory per CinemachineExtension's doc comment: this is what registers the extension
        // with the vcam pipeline.
        base.Awake();

        TryGetComponent(out _confiner);
        ResolveRoomBounds();

        if (_groundMask == 0)
        {
            _groundMask = LayerMask.GetMask("Ground", "Platform");
        }

        _ledgeFilter = new ContactFilter2D();
        _ledgeFilter.useTriggers = false;
        _ledgeFilter.SetLayerMask(_groundMask);
        _ledgeFilter.useLayerMask = true;

        _profileMissing = _profile == null;
    }

    private void Start()
    {
        // Deferred out of Awake so the log actually goes somewhere: the VContainer injection that
        // supplies _logger runs from the scene scope's build callback, whose ordering against this
        // component's Awake is undefined.
        if (_profileMissing)
        {
            _logger?.LogError($"[CameraForesightExtension] No CameraProfile assigned on '{name}' — disabling. The foresight solver throws on a null profile, so there is nothing safe to run.");
            enabled = false;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!LiveInstances.Contains(this))
        {
            LiveInstances.Add(this);
        }
    }

    private void OnDisable()
    {
        LiveInstances.Remove(this);

        // Nothing here should survive into the next enable: a re-enable is a fresh start, exactly
        // like coming back live from standby.
        _suspended = false;
        _biasCurrent = Vector2.zero;
        _zoomGate.Reset();
        _zoomDeltaCurrent = 0f;
        _lookDownCurrent = 0f;
        _ledgeDetected = false;
        _ledgeDropHeight = 0f;
        _bias.Reset();
    }

    /// <summary>
    /// Ledge probing lives here, not in the pipeline callback: this project's performance rules
    /// put physics queries in FixedUpdate, and Cinemachine's per-frame callback is the wrong place
    /// for a raycast. The callback only reads the cached result.
    /// </summary>
    private void FixedUpdate()
    {
        if (_suspended || _profile == null || _resolvedFollow == null)
        {
            _ledgeDetected = false;
            _ledgeDropHeight = 0f;
            return;
        }

        // Probe direction signal: the sign of horizontal velocity, holding the last non-zero sign
        // while idle. Deliberately the same "hold the last direction, never recentre" rule the bias
        // layer uses, so standing at the lip of a drop keeps revealing it — but read straight off
        // the body rather than from CameraDirectionalBias, which exposes no direction accessor and
        // is driven on the render tick, not this one.
        float velocityX = _followBody != null ? _followBody.linearVelocity.x : 0f;
        if (Mathf.Abs(velocityX) > HORIZONTAL_EPSILON)
        {
            _probeSign = velocityX > 0f ? 1 : -1;
        }

        Vector2 playerPosition = _resolvedFollow.position;
        Vector2 origin = playerPosition + new Vector2(_probeSign * _profile.LedgeProbeDistance, 0f);
        float maxDepth = Mathf.Max(0.01f, _profile.LedgeMinDropHeight * LEDGE_PROBE_DEPTH_MULTIPLIER);

        int hitCount = Physics2D.Raycast(origin, Vector2.down, _ledgeFilter, _ledgeHits, maxDepth);

        float highestFloorY = float.MinValue;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = _ledgeHits[index];
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.point.y > highestFloorY)
            {
                highestFloorY = hit.point.y;
            }
        }

        if (highestFloorY > float.MinValue)
        {
            _ledgeDetected = true;
            // Negative when the ground ahead is HIGHER than the player; the solver's minimum-drop
            // test rejects that, so no special case is needed here.
            _ledgeDropHeight = playerPosition.y - highestFloorY;
        }
        else
        {
            // No floor within reach: the drop is at least maxDepth, which by construction clears
            // the profile's minimum. Report it as exactly that rather than as "no ledge".
            _ledgeDetected = true;
            _ledgeDropHeight = maxDepth;
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body)
        {
            return;
        }

        // Defensive despite the Awake/Start disable: the enabled check that gates this callback
        // lives on the vcam's extension list, and a null profile would make the solver throw.
        if (_suspended || _profile == null)
        {
            return;
        }

        ResolveFollowTargets(vcam);

        // Follow can legitimately be null (CameraZoneTrigger clears it on exit). Treat that as
        // "at rest" rather than skipping the frame: the bias layer's job is to HOLD its last
        // direction when idle, and skipping would instead freeze a stale correction in place.
        float speedFactor = _movement != null ? _movement.SpeedFactor : 0f;
        Vector2 velocity = _followBody != null ? _followBody.linearVelocity : Vector2.zero;

        Bounds roomBounds = default;
        bool hasBounds = TryGetRoomBounds(out roomBounds);

        Vector2 cameraCenter = state.GetCorrectedPosition();

        // --- Zoom first: the frustum used to clamp the bias must be the one actually applied. ---
        // Gated through CameraZoomGate (not the raw speedFactor) so momentum zoom engages/disengages
        // with the same gate+dwell anti-flicker bias uses for direction, while the resulting size
        // itself stays continuous — see CameraZoomGate's own doc comment for why it recedes toward
        // baseline at rest instead of holding, unlike bias.
        float gatedZoomSpeedFactor = _zoomGate.Update(_profile, speedFactor, Mathf.Max(0f, deltaTime));
        float appliedOrthoSize = ApplyZoom(ref state, gatedZoomSpeedFactor, cameraCenter, hasBounds, roomBounds, Mathf.Max(0f, deltaTime));

        // --- Then the offsets, against that already-clamped frustum. ---
        Vector2 biasTarget = _bias.Update(_profile, velocity, speedFactor, Mathf.Max(0f, deltaTime));
        UpdateBias(biasTarget, Mathf.Max(0f, deltaTime));

        UpdateLookDown(Mathf.Max(0f, deltaTime));

        // World is +Y up throughout this project (MovementComponent's ground cast comment and
        // jumpSpeed > 0 both confirm it), so looking down is negative Y.
        Vector2 requestedOffset = new Vector2(_biasCurrent.x, -_lookDownCurrent);

        if (hasBounds)
        {
            float aspect = Mathf.Max(0.0001f, state.Lens.Aspect);
            float halfFrustumHeight = appliedOrthoSize;
            float halfFrustumWidth = appliedOrthoSize * aspect;

            // Bias and look-down are clamped as ONE offset vector rather than separately: both are
            // foresight displacements of the same camera, and ClampBias scales uniformly, so
            // combining shortens the composite offset without skewing its direction. Clamping only
            // the horizontal part would leave the vertical one free to push the frustum out of the
            // room — precisely the hole this safety net exists to close.
            requestedOffset = CameraConfinerClamp.ClampBias(
                requestedOffset,
                cameraCenter,
                halfFrustumWidth,
                halfFrustumHeight,
                roomBounds,
                _profile.ConfinerMargin);
        }

        state.PositionCorrection += new Vector3(requestedOffset.x, requestedOffset.y, 0f);
    }

    /// <summary>
    /// Notification that this vcam is going live. Cinemachine's StandbyUpdate defaults to
    /// RoundRobin, so a non-live vcam's state (including the bias hysteresis and the smoothed
    /// look-down) is arbitrarily stale by the time it comes back — resuming from it would snap
    /// the camera. Wipe everything and re-establish from live samples.
    /// </summary>
    public override bool OnTransitionFromCamera(ICinemachineCamera fromCam, Vector3 worldUp, float deltaTime)
    {
        bool requestUpdate = base.OnTransitionFromCamera(fromCam, worldUp, deltaTime);

        _bias.Reset();
        _biasCurrent = Vector2.zero;
        _zoomGate.Reset();
        _zoomDeltaCurrent = 0f;
        _lookDownCurrent = 0f;
        _ledgeDetected = false;
        _ledgeDropHeight = 0f;

        // Base returns false ("no vcam state update needed") and that is correct here: the reset
        // state is recomputed from scratch by the very next pipeline pass, so nothing is pending.
        return requestUpdate;
    }

    /// <summary>
    /// Contributes this frame's momentum zoom and returns the orthographic size actually applied
    /// (needed for the frustum the bias is clamped against).
    ///
    /// <paramref name="speedFactor"/> here is already gated through <see cref="CameraZoomGate"/> by
    /// the caller, not the raw MovementComponent value — see that type for the gate+dwell hysteresis.
    ///
    /// DELTA, NOT ABSOLUTE — the single most important correctness decision in this file:
    ///
    /// CameraForesightSolver.ComputeZoomOrthoSize returns an ABSOLUTE size in
    /// [profile.MinOrthographicSize, profile.MaxOrthographicSize], where Min is documented as the
    /// vcam's authored at-rest baseline. But that baseline is not the only thing writing this
    /// field: RoomLockTrigger lerps the vcam's serialized Lens to 10 for the boss room, and
    /// CameraZoomTrigger lerps it to 6 inside a zoom zone — both absolute writes to the vcam
    /// component, which is what seeds state.Lens.OrthographicSize before this callback runs.
    /// Writing the solver's absolute result here would therefore stomp both of them: entering a
    /// zoom zone at rest would snap 6 back to the profile's 8.
    ///
    /// So what is applied is the solver's result expressed as a distance from the at-rest
    /// baseline — "how much wider than standing still does this momentum want?" — added on top of
    /// whatever absolute size the state already carries. At speedFactor 0 the delta is exactly 0,
    /// so at rest the camera is bit-for-bit whatever RoomLockTrigger/CameraZoomTrigger asked for,
    /// including mid-lerp; at full momentum it is +(Max - Min) on top of that.
    ///
    /// The room clamp is then applied to the resulting ABSOLUTE size, with the pre-existing size
    /// as the floor: the clamp may only take back this class's own contribution, never eat into
    /// the size another script deliberately asked for.
    ///
    /// The delta itself is smoothed (<see cref="CameraProfile.ZoomSmoothTime"/>), not the resulting
    /// absolute size: SpeedFactor can step instantly rather than ramp — Dash sets
    /// Rigidbody2D.linearVelocity directly to 240 u/s in a single frame (DashHandler.DashCoroutine),
    /// nowhere near MovementComponent's run-speed-based acceleration curve — so the raw target delta
    /// jumps 0 -&gt; max on essentially every dash. Smoothing the combined absolute value instead would
    /// also drag out whatever RoomLockTrigger/CameraZoomTrigger are independently lerping toward;
    /// smoothing just this class's own delta leaves their transitions untouched.
    /// </summary>
    private float ApplyZoom(ref CameraState state, float speedFactor, Vector2 cameraCenter, bool hasBounds, Bounds roomBounds, float deltaTime)
    {
        float baselineOrthoSize = state.Lens.OrthographicSize;
        float momentumDeltaTarget = Mathf.Max(0f, CameraForesightSolver.ComputeZoomOrthoSize(_profile, speedFactor) - _profile.MinOrthographicSize);

        float smoothDuration = _profile.ZoomSmoothTime;
        if (smoothDuration <= 0f)
        {
            _zoomDeltaCurrent = momentumDeltaTarget;
        }
        else
        {
            float fullRange = Mathf.Max(0.0001f, _profile.MaxOrthographicSize - _profile.MinOrthographicSize);
            float rate = fullRange / smoothDuration;
            _zoomDeltaCurrent = Mathf.MoveTowards(_zoomDeltaCurrent, momentumDeltaTarget, rate * deltaTime);
        }

        float requestedOrthoSize = baselineOrthoSize + _zoomDeltaCurrent;

        if (hasBounds)
        {
            requestedOrthoSize = CameraConfinerClamp.ClampMaxOrthoSize(
                requestedOrthoSize,
                baselineOrthoSize,
                cameraCenter,
                Mathf.Max(0.0001f, state.Lens.Aspect),
                roomBounds,
                _profile.ConfinerMargin);
        }

        state.Lens.OrthographicSize = requestedOrthoSize;
        return requestedOrthoSize;
    }

    /// <summary>
    /// Drives the smoothed bias offset toward <see cref="CameraDirectionalBias.Update"/>'s raw
    /// target. That type outputs an instant all-or-nothing value by design (its own doc comment:
    /// damping is the consumer's job) — reported as a real jerk during playtesting (running one way
    /// then immediately reversing snapped the camera the full BiasMaxDistance-to-BiasMaxDistance
    /// distance in a single frame, since nothing between it and CameraState.PositionCorrection was
    /// smoothing anything). Rate covers the full swing (2x BiasMaxDistance) in BiasSmoothTime
    /// seconds, same "full distance over a duration" convention as <see cref="UpdateLookDown"/>.
    /// </summary>
    private void UpdateBias(Vector2 target, float deltaTime)
    {
        float duration = _profile.BiasSmoothTime;
        if (duration <= 0f)
        {
            _biasCurrent = target;
            return;
        }

        float rate = Mathf.Max(0.0001f, _profile.BiasMaxDistance * 2f) / duration;
        _biasCurrent = Vector2.MoveTowards(_biasCurrent, target, rate * deltaTime);
    }

    /// <summary>
    /// Drives the smoothed look-down offset toward the solver's instantaneous target. The solver
    /// is deliberately unsmoothed, so the react / recover rates are owned here — separate rates,
    /// because panning down should be quicker than coming back up.
    /// </summary>
    private void UpdateLookDown(float deltaTime)
    {
        float target = CameraForesightSolver.ComputeLookDownOffset(_profile, _ledgeDetected, _ledgeDropHeight);

        bool increasing = target > _lookDownCurrent;
        float duration = increasing ? _profile.LookDownReactTime : _profile.LookDownRecoverTime;

        if (duration <= 0f)
        {
            _lookDownCurrent = target;
            return;
        }

        // Rate is expressed as "full offset in `duration` seconds", so the timing stays honest
        // regardless of how far the current value happens to be from the target.
        float rate = Mathf.Max(0.0001f, _profile.LookDownOffset) / duration;
        _lookDownCurrent = Mathf.MoveTowards(_lookDownCurrent, target, rate * deltaTime);
    }

    /// <summary>
    /// Re-resolves the player components only when the Follow reference itself changes. A
    /// reference comparison per frame is cheap; a GetComponent per frame is forbidden by this
    /// project's performance rules.
    /// </summary>
    private void ResolveFollowTargets(CinemachineVirtualCameraBase vcam)
    {
        Transform follow = vcam != null ? vcam.Follow : null;
        if (follow == _resolvedFollow)
        {
            return;
        }

        _resolvedFollow = follow;

        if (follow == null)
        {
            _movement = null;
            _followBody = null;
            return;
        }

        // Follow may be the player root or a camera-follow child, so search upward too.
        _movement = follow.GetComponentInParent<MovementComponent>();
        _followBody = follow.GetComponentInParent<Rigidbody2D>();
    }

    /// <summary>
    /// Resolves and caches the room bounds once, in Awake — called once per scene load since the
    /// room border never moves at runtime, so there is nothing to recompute per frame.
    ///
    /// Reads the sibling CinemachineConfiner2D's BoundingShape2D as a black box (no
    /// InvalidateLensCache, no internals) with one necessary exception: every room border collider
    /// in this project is deliberately disabled (or its GameObject is inactive) to keep it out of
    /// physics while Cinemachine's own confiner bakes from the collider's points/transform, not
    /// Collider2D.bounds. Collider2D.bounds returns a zero-extent AABB for a disabled collider —
    /// confirmed against this project's own scenes, not assumed — so a degenerate result here falls
    /// back to reconstructing the AABB straight from the collider's authored geometry.
    /// </summary>
    private void ResolveRoomBounds()
    {
        Collider2D shape = _confiner != null ? _confiner.BoundingShape2D : null;
        if (shape == null)
        {
            _hasCachedRoomBounds = false;
            return;
        }

        Bounds bounds = shape.bounds;
        if (bounds.size.sqrMagnitude <= 0.0001f && !TryReconstructBounds(shape, out bounds))
        {
            _hasCachedRoomBounds = false;
            return;
        }

        _cachedRoomBounds = bounds;
        _hasCachedRoomBounds = true;
    }

    /// <summary>
    /// Rebuilds a collider's world-space AABB from its own geometry rather than
    /// <see cref="Collider2D.bounds"/>, which is unreliable on a disabled collider. Rotation-aware
    /// (transforms every corner/point individually rather than scaling a local size), verified
    /// against this project's own disabled room borders to reproduce the same extents an active
    /// collider's own <c>.bounds</c> would report.
    /// </summary>
    /// <returns>False for any collider type not handled below — callers fall back to "unclamped".</returns>
    private static bool TryReconstructBounds(Collider2D collider, out Bounds bounds)
    {
        Transform colliderTransform = collider.transform;

        BoxCollider2D box = collider as BoxCollider2D;
        if (box != null)
        {
            Vector2 half = box.size * 0.5f;
            Vector2 center = box.offset;
            bounds = new Bounds(colliderTransform.TransformPoint(center + new Vector2(-half.x, -half.y)), Vector3.zero);
            bounds.Encapsulate(colliderTransform.TransformPoint(center + new Vector2(half.x, -half.y)));
            bounds.Encapsulate(colliderTransform.TransformPoint(center + new Vector2(half.x, half.y)));
            bounds.Encapsulate(colliderTransform.TransformPoint(center + new Vector2(-half.x, half.y)));
            return true;
        }

        PolygonCollider2D polygon = collider as PolygonCollider2D;
        if (polygon != null && polygon.points.Length > 0)
        {
            Vector2[] points = polygon.points;
            bounds = new Bounds(colliderTransform.TransformPoint(points[0] + polygon.offset), Vector3.zero);
            for (int pointIndex = 1; pointIndex < points.Length; pointIndex++)
            {
                bounds.Encapsulate(colliderTransform.TransformPoint(points[pointIndex] + polygon.offset));
            }
            return true;
        }

        bounds = default;
        return false;
    }

    /// <returns>
    /// False when there is no confiner, its BoundingShape2D is empty (some vcams in this project
    /// genuinely are, e.g. the second one in 2_Under_City_One — the scene author leaving that
    /// camera unconfined on purpose), or its shape could not be resolved to a usable AABB. In every
    /// false case there is no bounds data to clamp against, so the request passes through unclamped.
    /// </returns>
    private bool TryGetRoomBounds(out Bounds bounds)
    {
        bounds = _cachedRoomBounds;
        if (_hasCachedRoomBounds)
        {
            return true;
        }

        if (!_confinerWarningLogged)
        {
            _confinerWarningLogged = true;
            _logger?.LogWarning($"[CameraForesightExtension] '{name}' has no usable room bounds (missing BoundingShape2D or an unresolvable collider shape) — foresight bias and zoom run unclamped on this camera.");
        }

        return false;
    }

    private void SetSuspended(bool suspended)
    {
        if (_suspended == suspended)
        {
            return;
        }

        _suspended = suspended;

        if (suspended)
        {
            // Leave nothing behind for the resume to snap out of.
            _bias.Reset();
            _biasCurrent = Vector2.zero;
            _zoomGate.Reset();
            _zoomDeltaCurrent = 0f;
            _lookDownCurrent = 0f;
            _ledgeDetected = false;
            _ledgeDropHeight = 0f;
        }
    }
}
