using ILogger = Core.Logging.ILogger;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using Core.Logging;
using VContainer;
using System.Collections;

namespace Game.Components.Movement
{
    public class MovementComponent : MonoBehaviour
    {
        private ILogger _logger;

        #region Movement Settings

        [Header("Movement Settings")]
        [FormerlySerializedAs("moveSpeed")]
        [SerializeField] private float maxSpeed = 8f;
        [SerializeField] private float acceleration = 8f;
        [SerializeField] private float deceleration = 44f;

        [Header("Direction Change Physics")]
        [SerializeField] private float reverseGroundBrake = 120f;
        [SerializeField] private float reverseAirBrake = 70f;
        [SerializeField] private float reverseSpeedThreshold = 0.25f;

        [Header("Speed Impact Settings")]
        [FormerlySerializedAs("momentumLossOnHit")]
        [SerializeField] private float speedLossOnHit = 0.3f;
        [FormerlySerializedAs("momentumLossOnWallCrash")]
        [SerializeField] private float speedLossOnWallCrash = 0.2f;

        [Header("Speed Based Action Boost")]
        [SerializeField] private float jumpAtMaxSpeedMultiplier = 1.2f;
        [SerializeField] private float dashAtMaxSpeedMultiplier = 1.45f;

        [Header("Air Control")]
        [SerializeField] private float airAcceleration = 30f;
        [SerializeField] private float airDeceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.8f;

        [Header("Post Dash Air Control")]
        [SerializeField] private float postDashAirBrakeTime = 0.25f;
        [SerializeField] private float postDashAirBrakeStrength = 120f;
        [SerializeField, Range(0.5f, 2f)] private float postDashMaxAirSpeedMultiplier = 1.15f;

        [Header("Jump Settings (Celeste-inspired)")]
        [SerializeField] private float jumpSpeed = -105f;
        [SerializeField] private float jumpHBoost = 40f;
        [SerializeField] private float jumpGraceTime = 0.1f;

        [Header("Gravity Settings")]
        [SerializeField] private float gravity = 900f;
        [SerializeField] private float maxFall = 160f;

        [Header("Advanced Jump")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [FormerlySerializedAs("lowJumpGravityMultiplier")]
        [SerializeField] private float risingGravityMultiplier = 3f;
        [SerializeField] private float fallAirControlMultiplier = 0.75f;

        [Header("Wall Stick")]
        [SerializeField] private float wallSlideMaxFallSpeed = 90f;
        [SerializeField] private float wallSlideAcceleration = 160f;
        [SerializeField] private float wallStickHorizontalBrake = 120f;
        [SerializeField] private float wallInputThreshold = 0.1f;

        [Header("Dash Settings")]
        [SerializeField] private DashSettings dashSettings = new DashSettings();

        [Header("Ground Check")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask oneWayPlatformMask;
        [SerializeField] private float dropThroughSafetyTime = 0.75f;

        [Header("Slope Handling")]
        [Tooltip("Surfaces up to this angle (deg) count as walkable ground. Steeper is treated as not-grounded.")]
        [SerializeField] private float maxSlopeAngle = 45f;
        [Tooltip("How far below the feet the ground CircleCast probes. Doubles as the snap/forgiveness window that keeps small bumps and slope vertices grounded instead of reading 'fall'.")]
        [SerializeField] private float groundSnapDistance = 0.12f;
        [Tooltip("Skin so the ground CircleCast radius sits just inside the capsule width (avoids grabbing side walls).")]
        [SerializeField] private float groundCheckSkin = 0.02f;
        [Tooltip("Seconds after a jump/bounce during which slope-stick + snap are suppressed so the launch can actually leave the ground.")]
        [SerializeField] private float postJumpSnapSuppressTime = 0.1f;

        [Header("Knockback Settings")]
        [Tooltip("Seconds after taking enemy damage during which move input and braking are ignored so the knockback velocity carries.")]
        [FormerlySerializedAs("knockbackLockTime")]
        [SerializeField] private float damageKnockbackLockTime = 0.2f;
        [Tooltip("Seconds after a dash-attack bounce (BounceFromDashImpact, player-initiated) during which move input and braking are ignored so the bounce velocity carries.")]
        [SerializeField] private float bounceKnockbackLockTime = 0.2f;

        [Header("Wall Jump Settings")]
        [SerializeField] private float wallJumpHorizontalSpeed = 80f;
        [SerializeField] private float wallJumpVerticalSpeed = -105f;
        [Tooltip("Seconds after a wall jump during which wall contact is ignored so the launch carries the player away.")]
        [SerializeField] private float wallJumpLockTime = 0.15f;
        [Tooltip("Seconds the same wall side is ignored after a straight-up (into-wall) jump, preventing spam climbing.")]
        [SerializeField] private float sameWallLockTime = 0.35f;

        [Header("Wall Check")]
        [SerializeField] private Vector2 wallCheckSize = new Vector2(0.2f, 0.8f);
        [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.35f, 0f);
        [SerializeField] private LayerMask climbableLayer;
        [Tooltip("When false, wall-slide and wall-jump are disabled (used by the tutorial to gate the wall ability). Default true so normal gameplay is unaffected.")]
        [SerializeField] private bool wallEnabled = true;

        [Header("Grab Settings")]
        [SerializeField] private LayerMask grabbableLayer;
        [SerializeField] private float grabCheckRadius = 1.5f;
        [SerializeField] private float grabLaunchBaseSpeed = 200f;
        [SerializeField, Range(0.5f, 1f)] private float grabLaunchMinMultiplier = 0.6f;
        [SerializeField, Range(1f, 2f)] private float grabLaunchMaxMultiplier = 1.4f;
        [SerializeField] private bool autoGrab = true;
        [SerializeField] private float grabLeapDuration = 0.1f;

        [Header("Animation")]
        [Tooltip("Minimum vertical speed (units/s) before the airborne anim counts as rising vs falling.")]
        [SerializeField] private float airAnimSpeedThreshold = 1f;

        #endregion

        #region Private State

        // Jump State
        private bool isJumping;
        private bool isFalling;
        private float varJumpSpeed;
        private float varJumpTimer;
        private float jumpGraceTimer;
        private bool autoJump;
        private float autoJumpTimer;
        private const float bounceAutoJumpTime = 0.1f;
        private const float jumpDirectionThreshold = 0.01f;
        private const float jumpBoostMinHorizontalSpeed = 0.5f;
        private const float bounceVarJumpTime = 0.2f;

        // Core References
        private Rigidbody2D rb;
        private Transform characterTransform;
        private Transform groundCheck;
        private bool isGrounded;
        [SerializeField] private bool canMove = true;
        private bool isTouchingWall;
        private int wallSideSign;
        private bool _wasWallSliding;
        private float _currentWallSlideSpeed;
        private Vector2 moveInput;
        private float _wallJumpLockTimer;
        private float _leftWallLockTimer;
        private float _rightWallLockTimer;
        private float _knockbackLockTimer;
        private float _bounceKnockbackLockTimer;
        private bool _isDashAttacking;

        // Dash (composed, not inherited)
        private DashHandler _dashHandler;
        private Coroutine _dashCoroutine;
        private float _postDashAirBrakeTimer;
        private bool _wasDashingLastFrame;

        [Header("Air Dash Refresh")]
        [SerializeField] private bool enableWallDashRefresh = true;
        [SerializeField] private bool enableHangDashRefresh = true;
        private readonly DashRefreshGate _wallDashGate = new DashRefreshGate();
        private readonly DashRefreshGate _hangDashGate = new DashRefreshGate();

        // Drop-through state
        private Collider2D _bodyCollider;
        private Collider2D _currentGroundCollider;
        private Collider2D _bestGroundCollider;
        private static readonly Collider2D[] _groundHitBuffer = new Collider2D[8];
        private ContactFilter2D _groundFilter;

        // Slope / ground-cast state
        private static readonly RaycastHit2D[] _groundCastBuffer = new RaycastHit2D[8];
        private ContactFilter2D _groundCastFilter;
        private bool _groundCastFilterReady;
        private Vector2 _groundNormal = Vector2.up;
        private Vector2 _groundPoint;
        private float _groundSlopeAngle;
        private bool _onSlope;
        private float _snapSuppressTimer;
        private float _lastGroundSpeed;

        // Grab state
        private bool isGrabbing;
        private bool isCaptured;
        private float _stunTimer;
        private bool _movementHalted;
        private SwingPoint currentSwingPoint;
        private float grabbedSpeedFactor;
        private float _autoGrabCooldownTimer;
        private bool _isLeapingToGrab;
        private Vector2 _leapStartPos;
        private float _leapTimer;

        #endregion

        #region Properties

        /// <summary>Raised the instant a ground jump launches. Use for one-shot feedback (sfx/vfx).</summary>
        public event Action Jumped;
        /// <summary>Raised the instant a wall jump launches. Use for one-shot feedback (sfx/vfx).</summary>
        public event Action WallJumped;
        /// <summary>Raised whenever a grab succeeds (manual or auto). Use for one-shot feedback (sfx/slow-motion).</summary>
        public event Action GrabStarted;

        public bool IsDashing => _dashHandler != null && _dashHandler.IsDashing;
        public bool IsDamageKnocked => _knockbackLockTimer > 0f;
        public bool IsBounceKnocked => _bounceKnockbackLockTimer > 0f;
        public bool IsKnocked => IsDamageKnocked || IsBounceKnocked;
        public bool DashAttacking => _dashHandler != null && _dashHandler.DashAttacking;
        public bool IsDashAttacking => _isDashAttacking;
        public bool CanDash => _dashHandler != null && _dashHandler.CanDash;
        public int CurrentDashes => _dashHandler?.CurrentDashes ?? 0;
        public int MaxDashes => _dashHandler?.MaxDashes ?? 0;
        public float MaxSpeed => maxSpeed;

        // Animation-facing airborne state. Derives "up" from the sign of jumpSpeed so it is
        // correct regardless of the project's vertical convention (and survives Inspector overrides).
        private float UpSign => Mathf.Sign(jumpSpeed != 0f ? jumpSpeed : -1f);
        private bool IsFallingAlongGravity => rb != null && rb.linearVelocity.y * UpSign < 0f;
        // Wall stick is intentional: holding a direction away from the wall does NOT peel you
        // off. The only ways to leave are a wall jump or sliding all the way down to the ground.
        public bool IsWallSliding => wallEnabled && isTouchingWall && !isGrounded && !isGrabbing && !IsDashing
                                     && IsFallingAlongGravity;
        public bool IsAirborne => rb != null && !isGrounded && !IsWallSliding && !isGrabbing && !IsDashing;
        public bool IsRisingAnim => IsAirborne
                                    && Mathf.Sign(rb.linearVelocity.y) == UpSign
                                    && Mathf.Abs(rb.linearVelocity.y) > airAnimSpeedThreshold;
        public bool IsFallingAnim => IsAirborne && !IsRisingAnim;

        public bool IsGrabbing => isGrabbing;
        public bool IsCaptured => isCaptured;
        public bool CanGrab => !isGrabbing && FindGrabTarget() != null;

        public void SetCaptured(bool value) { isCaptured = value; }

        public void DriveCapturedPosition(Vector2 worldPos)
        {
            if (rb != null) rb.MovePosition(worldPos);
        }

        public bool IsStunned => _stunTimer > 0f;

        public void Stun(float duration)
        {
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        public float SpeedFactor => GetCurrentSpeedFactorFromVelocity();
        public int WallSideSign => wallSideSign;
        public Vector2 DashDirection => _dashHandler?.DashDir ?? Vector2.zero;

        public bool AutoJump
        {
            get => autoJump;
            set => autoJump = value;
        }

        public bool AutoGrab
        {
            get => autoGrab;
            set => autoGrab = value;
        }

        #endregion

        #region Initialization

        [Inject]
        public void Construct(LoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<MovementComponent>();
            _logger?.Log("MovementComponent injected via VContainer");
        }

        private void Awake()
        {
            if (groundLayer == 0)
            {
                groundLayer = LayerMask.GetMask("Ground");
            }

            if (climbableLayer == 0)
            {
                climbableLayer = LayerMask.GetMask("Climbable");
            }

            if (grabbableLayer == 0)
            {
                grabbableLayer = LayerMask.GetMask("Grabbable");
            }

            if (oneWayPlatformMask == 0)
            {
                oneWayPlatformMask = LayerMask.GetMask("Platform");
            }

        }

        private void Start()
        {
            if (_logger == null)
                Debug.LogWarning("[MovementComponent] Logger not injected — check VContainer scope registration");
        }

        /// <summary>
        /// Initialize component with required dependencies
        /// </summary>
        public void Initialize(Rigidbody2D rigidbody, Transform transform)
        {
            rb = rigidbody;
            characterTransform = transform;
            canMove = true;
            isTouchingWall = false;
            wallSideSign = 0;
            _wasWallSliding = false;
            _currentWallSlideSpeed = 0f;
            moveInput = Vector2.zero;
            _wallJumpLockTimer = 0f;
            _leftWallLockTimer = 0f;
            _rightWallLockTimer = 0f;
            _knockbackLockTimer = 0f;
            _bounceKnockbackLockTimer = 0f;
            _postDashAirBrakeTimer = 0f;
            _wasDashingLastFrame = false;
            isGrabbing = false;
            isCaptured = false;
            _stunTimer = 0f;
            currentSwingPoint = null;
            grabbedSpeedFactor = 0f;
            _isLeapingToGrab = false;
            _leapTimer = 0f;

            if (rb != null)
            {
                if (rb.gravityScale != 0)
                {
                    _logger?.LogWarning($"Rigidbody2D.gravityScale was {rb.gravityScale}, forcing to 0");
                    rb.gravityScale = 0;
                }

                _logger?.Log($"Rigidbody2D settings: gravityScale={rb.gravityScale}, bodyType={rb.bodyType}, mass={rb.mass}");
            }

            // Resolve body collider for drop-through filtering
            _bodyCollider = GetComponent<Collider2D>();

            // Pre-build the ground filter (immutable after init, no per-frame GC)
            _groundFilter = new ContactFilter2D();
            _groundFilter.SetLayerMask(groundLayer);
            _groundFilter.useTriggers = false;
            _groundFilter.useLayerMask = true;

            // Ground CircleCast filter covers both solid ground and one-way platforms so the
            // slope-aware check sees every surface the player can stand on.
            _groundCastFilter = new ContactFilter2D();
            _groundCastFilter.SetLayerMask(groundLayer | oneWayPlatformMask);
            _groundCastFilter.useTriggers = false;
            _groundCastFilter.useLayerMask = true;
            _groundCastFilterReady = true;
            _groundNormal = Vector2.up;
            _snapSuppressTimer = 0f;
            _lastGroundSpeed = 0f;

            // Initialize dash handler (composition)
            _dashHandler = new DashHandler(dashSettings);
            _dashHandler.Initialize(rb);
            _wallDashGate.Enabled = enableWallDashRefresh;
            _hangDashGate.Enabled = enableHangDashRefresh;

            SetupGroundCheck();

            _logger?.Log($"MovementComponent initialized for {transform.name}");
        }

        private void SetupGroundCheck()
        {
            groundCheck = characterTransform.Find("GroundCheck");

            if (groundCheck == null)
            {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.SetParent(characterTransform);
                groundCheckObj.transform.localPosition = new Vector3(0, -0.5f, 0);
                groundCheck = groundCheckObj.transform;
                _logger?.Log($"Auto-created GroundCheck for {characterTransform.name}");
            }
        }

        #endregion

        #region Update

        /// <summary>
        /// Call this in character's Update
        /// </summary>
        public void UpdateMovement()
        {
            if (_movementHalted)
            {
                // TEMP DIAGNOSTIC
                if (rb != null && rb.linearVelocity.y > 5f) Debug.Log($"[UpdateMovement] HALTED zeroed vel was {rb.linearVelocity:F1}");
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (_wallJumpLockTimer > 0f)
            {
                _wallJumpLockTimer = Mathf.Max(0f, _wallJumpLockTimer - Time.deltaTime);
            }

            if (_leftWallLockTimer > 0f)
                _leftWallLockTimer = Mathf.Max(0f, _leftWallLockTimer - Time.deltaTime);

            if (_rightWallLockTimer > 0f)
                _rightWallLockTimer = Mathf.Max(0f, _rightWallLockTimer - Time.deltaTime);

            if (_knockbackLockTimer > 0f)
            {
                _knockbackLockTimer = Mathf.Max(0f, _knockbackLockTimer - Time.deltaTime);
            }

            if (_bounceKnockbackLockTimer > 0f)
            {
                _bounceKnockbackLockTimer = Mathf.Max(0f, _bounceKnockbackLockTimer - Time.deltaTime);
            }

            if (_snapSuppressTimer > 0f)
            {
                _snapSuppressTimer = Mathf.Max(0f, _snapSuppressTimer - Time.deltaTime);
            }

            CheckGroundStatus();
            CheckWallStatus();

            if (_dashHandler != null)
            {
                _dashHandler.UpdateTimers();

                if (isGrounded)
                {
                    _dashHandler.RefillDash();
                    _wallDashGate.Recharge();
                    _hangDashGate.Recharge();
                }
            }

            bool isDashingNow = IsDashing;
            if (_wasDashingLastFrame && !isDashingNow && !isGrounded)
            {
                _postDashAirBrakeTimer = postDashAirBrakeTime;
                CancelUnwantedPostDashUpwardCarry();
            }

            _wasDashingLastFrame = isDashingNow;

            if (_postDashAirBrakeTimer > 0f)
            {
                _postDashAirBrakeTimer = Mathf.Max(0f, _postDashAirBrakeTimer - Time.deltaTime);
            }

            if (isDashingNow)
            {
                // Auto-grab interrupts an active dash when a SwingPoint is in range.
                if (autoGrab && canMove && !isGrabbing && _autoGrabCooldownTimer <= 0f && CanGrab)
                {
                    if (_dashCoroutine != null) { StopCoroutine(_dashCoroutine); _dashCoroutine = null; }
                    _dashHandler?.ForceEndDash();
                    _wasDashingLastFrame = false;
                    _postDashAirBrakeTimer = 0f;
                    TryStartGrab();
                    return;
                }
                ResetWallSlideState();
                return;
            }

            if (_autoGrabCooldownTimer > 0f)
                _autoGrabCooldownTimer -= Time.deltaTime;

            if (autoGrab && canMove && _autoGrabCooldownTimer <= 0f && !isGrabbing && !isGrounded && CanGrab)
            {
                TryStartGrab();
            }

            if (isGrabbing)
            {
                if (_isLeapingToGrab) UpdateGrabLeap();
                else if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (isCaptured)
            {
                // TEMP DIAGNOSTIC
                if (rb != null && rb.linearVelocity.y > 5f) Debug.Log($"[UpdateMovement] CAPTURED zeroed vel was {rb.linearVelocity:F1}");
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer < 0f) _stunTimer = 0f;
                // TEMP DIAGNOSTIC
                if (rb != null && rb.linearVelocity.y > 5f) Debug.Log($"[UpdateMovement] STUN zeroed vel was {rb.linearVelocity:F1}");
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (_isDashAttacking)
            {
                // TEMP DIAGNOSTIC
                if (rb != null && rb.linearVelocity.y > 5f) Debug.Log($"[UpdateMovement] DASH_ATTACK zeroed vel was {rb.linearVelocity:F1}");
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            // TEMP DIAGNOSTIC — log velocity before jump/slope processing if it looks like a bounce
            if (rb != null && _bounceKnockbackLockTimer > 0f && rb.linearVelocity.y > 5f)
                Debug.Log($"[UpdateMovement] post-checks vel={rb.linearVelocity:F1} isGrounded={isGrounded} bounceKnockLock={_bounceKnockbackLockTimer:F3}");

            UpdateJumpState();
            ApplyGroundedSlopeMovement();
        }

        #endregion

        #region Horizontal Movement

        public void Move(Vector2 direction)
        {
            moveInput = direction;

            if (IsDashing) return;
            if (isGrabbing) return;
            if (isCaptured) return;
            if (IsStunned) return;
            // During the knockback lock, leave velocity untouched so the knockback carries —
            // skip input force, deceleration, reverse/post-dash braking entirely.
            if (_knockbackLockTimer > 0f || _bounceKnockbackLockTimer > 0f) return;
            if (_isDashAttacking) return;
            if (!canMove || rb == null) return;

            // While stuck to a wall, horizontal velocity is owned entirely by
            // UpdateWallSlideVelocity (which brakes it to 0). Applying movement force here
            // would let the player simply walk off the wall, defeating the wall stick.
            if (IsWallSliding) return;

            float currentSpeed = rb.linearVelocity.x;

            if (!isGrounded && _postDashAirBrakeTimer > 0f)
            {
                ApplyPostDashAirBrake();
                currentSpeed = rb.linearVelocity.x;
            }

            bool isReversingDirection = IsReversingDirection(direction.x, currentSpeed);
            if (isReversingDirection)
            {
                ApplyReverseBrake();
                return;
            }

            float effectiveMoveSpeed = GetCurrentHorizontalSpeedLimit();
            float targetSpeed = direction.x * effectiveMoveSpeed;
            bool isPushingIntoWall = !isGrounded
                                     && isTouchingWall
                                     && wallSideSign != 0
                                     && Mathf.Abs(direction.x) >= wallInputThreshold
                                     && Mathf.Sign(direction.x) == wallSideSign;

            if (isPushingIntoWall)
            {
                // Do not let horizontal input "glue" the player against the wall.
                targetSpeed = 0f;
            }

            float accelRate;
            float controlMultiplier = 1f;

            if (isGrounded)
            {
                accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
            }
            else
            {
                accelRate = Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration;

                if (isFalling)
                {
                    controlMultiplier = fallAirControlMultiplier;
                }
                else
                {
                    controlMultiplier = airControlMultiplier;
                }

                targetSpeed *= controlMultiplier;

                // Keep airborne momentum when holding the same direction; do not auto-brake in mid-air.
                bool isHoldingSameAirDirection = Mathf.Abs(direction.x) > 0.01f
                                                && Mathf.Abs(currentSpeed) > 0.01f
                                                && Mathf.Sign(direction.x) == Mathf.Sign(currentSpeed);
                if (isHoldingSameAirDirection)
                {
                    targetSpeed = Mathf.Sign(direction.x) * Mathf.Max(Mathf.Abs(targetSpeed), Mathf.Abs(currentSpeed));
                }
            }

            float speedDiff = targetSpeed - currentSpeed;
            float movement = speedDiff * accelRate;

            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);

            if (isGrounded && Mathf.Abs(rb.linearVelocity.x) > effectiveMoveSpeed)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * effectiveMoveSpeed, rb.linearVelocity.y);
            }
        }

        public void Stop()
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }

        #endregion

        #region Dash

        public void Dash(Vector2 direction)
        {
            if (isGrabbing) return;
            if (isCaptured) return;
            if (IsStunned) return;
            if (_dashHandler == null || !_dashHandler.CanDash || direction == Vector2.zero) return;

            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
            }

            float speedFactor = GetCurrentSpeedFactorFromVelocity();
            float dashSpeedMultiplier = Mathf.Lerp(1f, dashAtMaxSpeedMultiplier, speedFactor);
            _dashCoroutine = StartCoroutine(DashCoroutineWrapper(direction, dashSpeedMultiplier));
        }

        private IEnumerator DashCoroutineWrapper(Vector2 direction, float speedMultiplier)
        {
            yield return StartCoroutine(_dashHandler.DashCoroutine(direction, isGrounded, speedMultiplier));
            _dashCoroutine = null;
        }

        public void ResetDash()
        {
            _dashHandler?.ResetDash();
        }

        // Combo refresh — makes the dash immediately available again after a weak-point / true-damage
        // dash hit. The next Dash() call interrupts the current dash coroutine on its own.
        public void RechargeDashForCombo()
        {
            _dashHandler?.RechargeForCombo();
        }

        #endregion

        #region Jump

        /// <summary>
        /// Jump - Celeste Style with Variable Jump Height + Jump Grace Time
        /// </summary>
        public void Jump(bool particles = true, bool playSfx = true)
        {
            _logger?.Log($"Jump() START - Current velocity: {rb?.linearVelocity}");

            if (!CanExecuteJump())
            {
                return;
            }

            if (IsWallSliding)
            {
                PerformWallJump();
                return;
            }

            ExecuteGroundJump();
        }

        public void CancelJump()
        {
            if (varJumpTimer > 0)
            {
                varJumpTimer = 0;
                _logger?.Log("Jump cancelled (released button)");
            }
        }

        /// <summary>
        /// Bounce - Celeste Style (for Springs / Bouncers)
        /// </summary>
        public void Bounce(float fromY, float bounceSpeed = -140f)
        {
            if (rb == null) return;

            float bottomY = characterTransform.position.y - (groundCheckSize.y / 2);
            MoveVExact((int)(fromY - bottomY));

            isJumping = false;
            isFalling = false;
            jumpGraceTimer = 0;
            varJumpTimer = bounceVarJumpTime;
            autoJump = true;
            autoJumpTimer = bounceAutoJumpTime;

            varJumpSpeed = bounceSpeed;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceSpeed);
            _snapSuppressTimer = postJumpSnapSuppressTime;

            _logger?.Log($"Bounced! Speed: {bounceSpeed}");
        }

        public void StartJumpGraceTime()
        {
            jumpGraceTimer = jumpGraceTime;
        }

        private void PerformWallJump()
        {
            // If the player is pressing INTO the wall, treat it as a straight-up jump (no push-off).
            // Pressing away or neutral uses the classic wall-jump launch.
            bool pressingIntoWall = wallSideSign != 0
                && Mathf.Abs(moveInput.x) >= wallInputThreshold
                && Mathf.Sign(moveInput.x) == wallSideSign;

            ResetWallSlideState();
            BeginJumpWindow();

            if (pressingIntoWall)
            {
                rb.linearVelocity = new Vector2(0f, Mathf.Abs(wallJumpVerticalSpeed) * UpSign);
                ApplyJumpStateAfterLaunch(rb.linearVelocity.y);
                _wallJumpLockTimer = wallJumpLockTime;
                // Lock the same side so the player can't immediately re-grab and spam climb.
                if (wallSideSign == -1) _leftWallLockTimer  = sameWallLockTime;
                else if (wallSideSign == 1) _rightWallLockTimer = sameWallLockTime;
                Jumped?.Invoke();
            }
            else
            {
                int jumpAwayDirection = wallSideSign != 0
                    ? -wallSideSign
                    : (characterTransform != null && characterTransform.localScale.x >= 0 ? -1 : 1);

                // Wall jump always launches in the "up" direction (UpSign, derived from jumpSpeed),
                // regardless of the serialized field's sign — a stale/negative value must never
                // fire the player into the ground.
                rb.linearVelocity = new Vector2(
                    jumpAwayDirection * wallJumpHorizontalSpeed,
                    Mathf.Abs(wallJumpVerticalSpeed) * UpSign
                );
                ApplyJumpStateAfterLaunch(rb.linearVelocity.y);
                _wallJumpLockTimer = wallJumpLockTime;
                WallJumped?.Invoke();
            }

            if (_wallDashGate.TryConsume(_dashHandler.CurrentDashes, _dashHandler.MaxDashes))
                _dashHandler.ResetDash();
        }

        private bool CanExecuteJump()
        {
            if (!canMove || rb == null)
            {
                _logger?.LogWarning($"Jump BLOCKED! canMove: {canMove}, rb: {rb != null}");
                return false;
            }

            if (isCaptured) return false;
            if (IsStunned) return false;

            if (IsWallSliding)
            {
                return true;
            }

            if (jumpGraceTimer <= 0f && !isGrounded)
            {
                _logger?.LogWarning($"Jump BLOCKED! jumpGraceTimer: {jumpGraceTimer:F3}, isGrounded: {isGrounded}");
                return false;
            }

            return true;
        }

        private void ExecuteGroundJump()
        {
            BeginJumpWindow();

            float currentSpeedX = rb.linearVelocity.x;
            float moveDirection = ResolveJumpDirection(currentSpeedX);
            float speedFactor = GetCurrentSpeedFactorFromVelocity();
            float jumpBoostAmount = CalculateJumpBoostAmount(currentSpeedX, moveDirection, speedFactor);
            float jumpVerticalSpeed = jumpSpeed * Mathf.Lerp(1f, jumpAtMaxSpeedMultiplier, speedFactor);

            Vector2 jumpVelocity = new Vector2(currentSpeedX + jumpBoostAmount, jumpVerticalSpeed);
            rb.linearVelocity = jumpVelocity;
            ApplyJumpStateAfterLaunch(jumpVelocity.y);

            Jumped?.Invoke();

            _logger?.Log($"Jump applied! speedX={currentSpeedX:F2}, speedFactor={speedFactor:F2}, " +
                         $"boost={jumpBoostAmount:F2}, vSpeed={jumpVerticalSpeed:F2}, velocity={jumpVelocity}");
        }

        // Standard jumps (ground / coyote / wall) only consume the coyote grace here.
        // They intentionally do NOT engage the variable-jump hold or autoJump-driven
        // half-gravity — jump power comes from momentum, not button-hold. That machinery
        // is reserved for Bounce() (springs), which sets its own autoJump/timers.
        private void BeginJumpWindow()
        {
            jumpGraceTimer = 0f;
            varJumpTimer = 0f;
            autoJump = false;
            // Suppress slope-stick/snap briefly so the launch can leave the ground instead of
            // being re-glued to the surface on the same/next frame.
            _snapSuppressTimer = postJumpSnapSuppressTime;
        }

        private float ResolveJumpDirection(float currentSpeedX)
        {
            if (Mathf.Abs(currentSpeedX) > jumpDirectionThreshold)
            {
                return Mathf.Sign(currentSpeedX);
            }

            if (Mathf.Abs(moveInput.x) > jumpDirectionThreshold)
            {
                return Mathf.Sign(moveInput.x);
            }

            return characterTransform != null && characterTransform.localScale.x >= 0f ? 1f : -1f;
        }

        private float CalculateJumpBoostAmount(float currentSpeedX, float moveDirection, float speedFactor)
        {
            if (Mathf.Abs(currentSpeedX) <= jumpBoostMinHorizontalSpeed)
            {
                return 0f;
            }

            float currentMaxHorizontalSpeed = GetCurrentHorizontalSpeedLimit();
            float speedBasedJumpBoost = jumpHBoost * Mathf.Lerp(1f, jumpAtMaxSpeedMultiplier, speedFactor);
            float maxBoost = Mathf.Min(speedBasedJumpBoost, currentMaxHorizontalSpeed - Mathf.Abs(currentSpeedX));
            return maxBoost * moveDirection;
        }

        private void ApplyJumpStateAfterLaunch(float verticalSpeed)
        {
            varJumpSpeed = verticalSpeed;
            isJumping = true;
            isFalling = false;
        }

        #endregion

        #region Jump State

        private void UpdateJumpState()
        {
            if (rb == null) return;

            if (HandleWallSlideJumpState()) return;

            ResetWallSlideState();

            if (HandleGroundedJumpState()) return;

            float deltaTime = Time.deltaTime;
            UpdateJumpTimers(deltaTime);
            ApplyVariableJumpHold(deltaTime);
            ApplyJumpGravity(deltaTime);
            UpdateAirborneJumpFlags();
        }

        private bool HandleWallSlideJumpState()
        {
            if (!IsWallSliding)
            {
                return false;
            }

            UpdateWallSlideVelocity();
            float gravityDirection = gravity >= 0f ? -1f : 1f;
            isFalling = rb.linearVelocity.y * gravityDirection > 0f;
            isJumping = false;
            return true;
        }

        private bool HandleGroundedJumpState()
        {
            if (!isGrounded)
            {
                return false;
            }

            isJumping = false;
            isFalling = false;
            jumpGraceTimer = jumpGraceTime;
            autoJump = false;
            return true;
        }

        private void UpdateJumpTimers(float deltaTime)
        {
            if (jumpGraceTimer > 0f)
            {
                jumpGraceTimer -= deltaTime;
            }

            if (autoJumpTimer <= 0f)
            {
                return;
            }

            if (!autoJump)
            {
                autoJumpTimer = 0f;
                return;
            }

            autoJumpTimer -= deltaTime;
            if (autoJumpTimer <= 0f)
            {
                autoJump = false;
            }
        }

        private void ApplyVariableJumpHold(float deltaTime)
        {
            if (varJumpTimer <= 0f)
            {
                return;
            }

            if (!autoJump)
            {
                varJumpTimer = 0f;
                return;
            }

            Vector2 velocity = rb.linearVelocity;
            // TEMP DIAGNOSTIC
            if (velocity.y > 5f && Mathf.Min(velocity.y, varJumpSpeed) < velocity.y)
                Debug.Log($"[VarJumpHold] capping vel.y {velocity.y:F1} → {varJumpSpeed:F1}");
            velocity.y = Mathf.Min(velocity.y, varJumpSpeed);
            rb.linearVelocity = velocity;
            varJumpTimer -= deltaTime;
        }

        private void ApplyJumpGravity(float deltaTime)
        {
            float gravityMultiplier = ResolveJumpGravityMultiplier();
            Vector2 velocity = rb.linearVelocity;
            velocity.y -= gravity * gravityMultiplier * deltaTime;

            if (velocity.y > maxFall)
            {
                velocity.y = maxFall;
            }

            rb.linearVelocity = velocity;
        }

        private float ResolveJumpGravityMultiplier()
        {
            float verticalSpeed = rb.linearVelocity.y;

            if (verticalSpeed > 0f)
            {
                return fallGravityMultiplier;
            }

            if (verticalSpeed < 0f)
            {
                return risingGravityMultiplier;
            }

            return 1f;
        }

        private void UpdateAirborneJumpFlags()
        {
            if (rb.linearVelocity.y > 0f)
            {
                isFalling = true;
                isJumping = false;
            }
        }

        #endregion

        #region Ground Check

        private void CheckGroundStatus()
        {
            bool previousGrounded = isGrounded;
            isGrounded = false;
            _currentGroundCollider = null;
            _bestGroundCollider = null;
            _groundNormal = Vector2.up;
            _groundPoint = Vector2.zero;
            _groundSlopeAngle = 0f;
            _onSlope = false;

            if (_bodyCollider != null && _groundCastFilterReady)
            {
                // A wide circle just under the feet: it glides over composite-collider "ghost
                // vertices" (the source of the floating bump) and returns the real surface normal
                // so gentle slopes no longer read as a fall. World is +Y up (jumpSpeed > 0).
                // Cast from the capsule CENTER (not the feet) so the circle does not start already
                // overlapping the ground — an overlapping start returns a zero normal and would hide
                // the slope. From the center it clears the surface, then descends to groundSnapDistance
                // below the feet (the forgiveness window).
                Bounds b = _bodyCollider.bounds;
                float radius = Mathf.Max(0.05f, b.extents.x - groundCheckSkin);
                Vector2 origin = b.center;
                float castDistance = Mathf.Max(0.01f, b.extents.y - radius + groundSnapDistance);

                int hitCount = Physics2D.CircleCast(origin, radius, Vector2.down, _groundCastFilter, _groundCastBuffer, castDistance);
                float bestAngle = float.MaxValue;
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit2D hit = _groundCastBuffer[i];
                    if (hit.collider == null) continue;
                    if (Physics2D.GetIgnoreCollision(_bodyCollider, hit.collider)) continue;

                    // A cast that begins overlapping returns a zero normal; fall back to "up".
                    Vector2 n = hit.normal.sqrMagnitude < 0.0001f ? Vector2.up : hit.normal;
                    float angle = Vector2.Angle(n, Vector2.up);
                    if (angle > maxSlopeAngle) continue; // too steep to stand on — treat as wall

                    bool isOneWay = ((1 << hit.collider.gameObject.layer) & oneWayPlatformMask.value) != 0;
                    // One-way platform: only counts as ground when feet are at/above the surface.
                    if (isOneWay && b.min.y < hit.collider.bounds.max.y - 0.02f)
                        continue;

                    isGrounded = true;
                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        _groundNormal = n;
                        _groundPoint = hit.point;
                        _groundSlopeAngle = angle;
                        _bestGroundCollider = hit.collider;
                    }

                    if (_currentGroundCollider == null && isOneWay)
                        _currentGroundCollider = hit.collider;
                }

                _onSlope = isGrounded && _groundSlopeAngle > 0.5f;
            }

            if (previousGrounded != isGrounded)
            {
                _logger?.Log($"Ground state changed: {previousGrounded} -> {isGrounded}");
            }

            if (previousGrounded && !isGrounded && rb != null && rb.linearVelocity.y >= 0)
            {
                StartJumpGraceTime();
                _logger?.Log("Started jump grace time (coyote time)");
            }
        }

        // Redirect ground movement along the slope and stick the capsule to the surface.
        // Runs at the end of UpdateMovement (grounded, non-dash, non-grab states only), so it has the
        // final say over the velocity the physics step will integrate.
        private void ApplyGroundedSlopeMovement()
        {
            if (rb == null || !isGrounded || _bodyCollider == null) return;
            if (_knockbackLockTimer > 0f || _bounceKnockbackLockTimer > 0f) return;   // let a knockback carry untouched
            if (_snapSuppressTimer > 0f)            // just jumped/bounced: don't re-stick the launch
            {
                _lastGroundSpeed = rb.linearVelocity.x;
                return;
            }

            // Tangent along the surface, oriented so +x means "moving right".
            Vector2 normal = _groundNormal;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            if (tangent.x < 0f) tangent = -tangent;

            // Speed ALONG the surface = project the full velocity onto the tangent. Using only the
            // horizontal component here would shrink it by cos(slope) every frame (the velocity gets
            // re-projected each tick), so walking a slope felt sluggish; the dot keeps the ground speed
            // intact so a slope feels exactly like flat ground. Clamp to the same grounded speed cap as
            // flat so it never reads faster either. Perpendicular (gravity) component is dropped → no
            // idle slide, no flying off the bottom of a ramp.
            float speedLimit = GetCurrentHorizontalSpeedLimit();
            float groundSpeed = Mathf.Clamp(Vector2.Dot(rb.linearVelocity, tangent), -speedLimit, speedLimit);
            _lastGroundSpeed = groundSpeed;
            Vector2 slopeVel = tangent * groundSpeed;

            // Hard-stick: close any small vertical gap to the surface this step (within forgiveness),
            // e.g. when cresting a convex slope. Velocity-based so it plays nice with interpolation and
            // the Update-driven velocity pipeline; never pushes the player up out of the ground.
            float dt = Time.deltaTime;
            if (dt > 0f && _groundPoint != Vector2.zero)
            {
                float gap = _bodyCollider.bounds.min.y - _groundPoint.y; // >0: feet above surface
                if (gap > 0.0001f && gap <= groundSnapDistance)
                    slopeVel.y -= gap / dt;
            }

            // TEMP DIAGNOSTIC
            if (rb.linearVelocity.y > 5f) Debug.Log($"[SlopeSnap] overwriting vel {rb.linearVelocity:F1} → {slopeVel:F1}");
            rb.linearVelocity = slopeVel;
        }

        private void CheckWallStatus()
        {
            if (characterTransform == null)
            {
                isTouchingWall = false;
                wallSideSign = 0;
                return;
            }

            if (_wallJumpLockTimer > 0f)
            {
                // During the wall-jump lock window, ignore wall contact so the launch
                // velocity carries the player away instead of being re-captured by the
                // climb / wall-slide logic on the very next frame.
                isTouchingWall = false;
                wallSideSign = 0;
                return;
            }

            Vector2 center = characterTransform.position;
            float offsetX = Mathf.Abs(wallCheckOffset.x);

            Vector2 leftCheckPos = center + new Vector2(-offsetX, wallCheckOffset.y);
            Vector2 rightCheckPos = center + new Vector2(offsetX, wallCheckOffset.y);

            bool leftHit  = _leftWallLockTimer  <= 0f && Physics2D.OverlapBox(leftCheckPos,  wallCheckSize, 0f, climbableLayer);
            bool rightHit = _rightWallLockTimer <= 0f && Physics2D.OverlapBox(rightCheckPos, wallCheckSize, 0f, climbableLayer);

            isTouchingWall = !isGrounded && (leftHit || rightHit);

            if (rightHit)
            {
                wallSideSign = 1;
            }
            else if (leftHit)
            {
                wallSideSign = -1;
            }
            else
            {
                wallSideSign = 0;
            }
        }

        private void UpdateWallSlideVelocity()
        {
            if (rb == null)
            {
                return;
            }

            float gravityDirection = gravity >= 0f ? -1f : 1f;
            float maxSlideSpeed = wallSlideMaxFallSpeed;
            float currentGravityAlignedSpeed = rb.linearVelocity.y * gravityDirection;

            if (!_wasWallSliding)
            {
                _currentWallSlideSpeed = Mathf.Max(0f, currentGravityAlignedSpeed);
            }
            else
            {
                _currentWallSlideSpeed += wallSlideAcceleration * Time.deltaTime;
            }

            _currentWallSlideSpeed = Mathf.Min(_currentWallSlideSpeed, maxSlideSpeed);

            float clampedX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, wallStickHorizontalBrake * Time.deltaTime);
            rb.linearVelocity = new Vector2(clampedX, gravityDirection * _currentWallSlideSpeed);
            _wasWallSliding = true;
        }

        private void ResetWallSlideState()
        {
            _wasWallSliding = false;
            _currentWallSlideSpeed = 0f;
        }

        #endregion

        #region Grab

        private SwingPoint FindGrabTarget()
        {
            if (characterTransform == null) return null;

            Collider2D[] hits = Physics2D.OverlapCircleAll(characterTransform.position, grabCheckRadius, grabbableLayer);
            SwingPoint nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                SwingPoint sp = hit.GetComponent<SwingPoint>();
                if (sp == null) continue;

                float dist = Vector2.Distance(characterTransform.position, sp.AnchorPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = sp;
                }
            }

            return nearest;
        }

        public bool TryStartGrab()
        {
            if (rb == null || characterTransform == null) return false;

            SwingPoint target = FindGrabTarget();
            if (target == null) return false;

            // Snapshot arrival speed BEFORE zeroing velocity — this is the launch-power source.
            grabbedSpeedFactor = GetCurrentSpeedFactorFromVelocity();

            currentSwingPoint = target;
            isGrabbing = true;

            // Start a short leap to the anchor instead of teleporting (avoids camera snap).
            // GrabStarted (slow-mo + sound) fires only when the leap reaches the anchor.
            _leapStartPos = rb.position;
            _leapTimer = 0f;
            _isLeapingToGrab = true;

            rb.linearVelocity = Vector2.zero;

            // Clear conflicting states.
            ResetWallSlideState();
            isJumping = false;
            isFalling = false;
            jumpGraceTimer = 0f;
            varJumpTimer = 0f;
            autoJump = false;

            _logger?.Log($"Grab leap started on {target.name}, speedFactor={grabbedSpeedFactor:F2}");
            return true;
        }

        private void UpdateGrabLeap()
        {
            if (currentSwingPoint == null)
            {
                _isLeapingToGrab = false;
                return;
            }

            _leapTimer += Time.unscaledDeltaTime;
            float t = grabLeapDuration > 0f ? Mathf.Clamp01(_leapTimer / grabLeapDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad

            Vector2 anchor = currentSwingPoint.AnchorPosition;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = Vector2.Lerp(_leapStartPos, anchor, eased);
            }

            if (t >= 1f)
            {
                _isLeapingToGrab = false;
                if (rb != null) rb.position = anchor;
                GrabStarted?.Invoke(); // triggers slow-mo + grab sound once anchored
            }
        }

        public void LaunchFromGrab(Vector2 aimDir)
        {
            if (rb == null || !isGrabbing) return;

            float speed = grabLaunchBaseSpeed * Mathf.Lerp(grabLaunchMinMultiplier, grabLaunchMaxMultiplier, grabbedSpeedFactor);
            rb.linearVelocity = aimDir.normalized * speed;

            currentSwingPoint = null;
            isGrabbing = false;
            _isLeapingToGrab = false;
            _autoGrabCooldownTimer = 0.4f;

            if (_hangDashGate.TryConsume(_dashHandler.CurrentDashes, _dashHandler.MaxDashes))
                _dashHandler.ResetDash();

            isJumping = true;
            isFalling = false;
            ResetWallSlideState();

            _logger?.Log($"Launch from grab: dir={aimDir}, speed={speed:F1}, factor={grabbedSpeedFactor:F2}");
        }

        public void ReleaseGrab()
        {
            if (!isGrabbing) return;

            currentSwingPoint = null;
            isGrabbing = false;
            _isLeapingToGrab = false;
            _autoGrabCooldownTimer = 0.4f;

            _logger?.Log("Grab released (drop)");
        }

        #endregion

        #region Utility

        public void DropThroughPlatform()
        {
            if (!isGrounded || _currentGroundCollider == null || _bodyCollider == null) return;
            if (Physics2D.GetIgnoreCollision(_bodyCollider, _currentGroundCollider)) return;
            StartCoroutine(DropRoutine(_currentGroundCollider));
        }

        private IEnumerator DropRoutine(Collider2D platformCol)
        {
            Physics2D.IgnoreCollision(_bodyCollider, platformCol, true);

            float elapsed = 0f;
            while (elapsed < dropThroughSafetyTime)
            {
                if (_bodyCollider.bounds.max.y < platformCol.bounds.min.y)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            Physics2D.IgnoreCollision(_bodyCollider, platformCol, false);
        }

        private void MoveVExact(int amount)
        {
            if (rb != null)
            {
                Vector2 pos = rb.position;
                pos.y += amount;
                rb.position = pos;
            }
        }

        public void SetSpeed(float speed)
        {
            maxSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>Enable/disable wall-slide and wall-jump (tutorial gating). Default is enabled.</summary>
        public void SetWallEnabled(bool value)
        {
            wallEnabled = value;
        }

        public void SetCanMove(bool value)
        {
            canMove = value;
            if (!canMove)
            {
                _postDashAirBrakeTimer = 0f;
                _wasDashingLastFrame = false;
            }
        }

        public void SetMovementHalted(bool value)
        {
            _movementHalted = value;
            if (_movementHalted && rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        public void ResetState()
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            ResetDash();
            _knockbackLockTimer = 0f;
            _bounceKnockbackLockTimer = 0f;
            _wallJumpLockTimer = 0f;
            _leftWallLockTimer = 0f;
            _rightWallLockTimer = 0f;
            _stunTimer = 0f;
            _isDashAttacking = false;
            isCaptured = false;
            if (isGrabbing) ReleaseGrab();
            _isLeapingToGrab = false;
            _movementHalted = false;
            SetCanMove(true);
        }

        public bool IsGrounded()
        {
            return isGrounded;
        }

        public string GetGroundTag()
        {
            return isGrounded && _bestGroundCollider != null ? _bestGroundCollider.tag : null;
        }

        public bool IsJumping()
        {
            return isJumping;
        }

        public bool IsFalling()
        {
            return isFalling;
        }

        public Vector2 GetVelocity()
        {
            return rb != null ? rb.linearVelocity : Vector2.zero;
        }

        public void SetVelocity(Vector2 velocity)
        {
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }
        }

        public void NotifyWallCrash()
        {
            if (rb != null)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * (1f - speedLossOnWallCrash), rb.linearVelocity.y);
        }

        public void NotifyDamageTaken()
        {
            if (rb != null)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * (1f - speedLossOnHit), rb.linearVelocity.y);
        }

        // Push the character away from sourcePosition. Horizontal dir is sign(playerX - sourceX);
        // vertical uses UpSign so upwardBias > 0 always reads as "up" regardless of vertical convention.
        // Velocity is replaced (not added) for a deterministic knock distance, and the lock window
        // suppresses Move() braking so the shove actually carries.
        public void ApplyKnockback(Vector2 sourcePosition, float force, float upwardBias)
        {
            if (rb == null || characterTransform == null) return;

            // Bounce has higher priority — preserve the rebound velocity during its window.
            if (_bounceKnockbackLockTimer > 0f) return;

            float dir = Mathf.Sign(characterTransform.position.x - sourcePosition.x);
            if (dir == 0f)
                dir = characterTransform.localScale.x >= 0f ? 1f : -1f;

            Vector2 kick = new Vector2(dir, UpSign * upwardBias).normalized * force;

            ResetWallSlideState();
            isGrabbing = false;
            _isLeapingToGrab = false;
            rb.linearVelocity = kick;
            _knockbackLockTimer = damageKnockbackLockTime;
        }

        public void BeginDashAttackFreeze()
        {
            if (rb == null) return;

            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
                _dashCoroutine = null;
            }
            _dashHandler?.ForceEndDash();
            _wasDashingLastFrame = false;
            _postDashAirBrakeTimer = 0f;
            ResetWallSlideState();
            isGrabbing = false;
            _isLeapingToGrab = false;

            rb.linearVelocity = Vector2.zero;
            _isDashAttacking = true;
        }

        public void EndDashAttackFreeze()
        {
            _isDashAttacking = false;
        }

        // Rebound the player opposite the dash direction on hitting an enemy.
        // Stops the running dash coroutine so its end-of-dash velocity write never fires,
        // then sets velocity to -dashDir (+ optional upward pop) scaled by dash momentum.
        // The knockback lock window suppresses Move() braking so the bounce carries.
        public void BounceFromDashImpact(float forceH, float forceV, float upwardBias)
        {
            if (rb == null) return;

            EndDashAttackFreeze();

            Vector2 dashDir = _dashHandler != null ? _dashHandler.DashDir : Vector2.zero;
            if (dashDir == Vector2.zero)
                dashDir = new Vector2(characterTransform != null ? Mathf.Sign(characterTransform.localScale.x) : 1f, 0f);

            float multiplier = _dashHandler != null ? _dashHandler.DashSpeedMultiplier : 1f;

            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
                _dashCoroutine = null;
            }
            _dashHandler?.ForceEndDash();
            _wasDashingLastFrame = false;

            Vector2 bounceDir = (-dashDir + Vector2.up * upwardBias).normalized;
            Vector2 finalVel = new Vector2(bounceDir.x * forceH, bounceDir.y * forceV) * multiplier;
            rb.linearVelocity = finalVel;

            // TEMP DIAGNOSTIC — remove after bug is identified
            Debug.Log($"[BounceFromDashImpact] dashDir={dashDir:F3} bounceDir={bounceDir:F3} finalVel={finalVel:F1} isGrounded={isGrounded} multiplier={multiplier:F2}");

            _bounceKnockbackLockTimer = bounceKnockbackLockTime;
            _snapSuppressTimer = postJumpSnapSuppressTime;
            _postDashAirBrakeTimer = 0f;
            // Clear any pending jump-hold state so the bounce velocity isn't capped by ApplyVariableJumpHold
            varJumpTimer = 0f;
            autoJump = false;
            ResetWallSlideState();
            isGrabbing = false;
            _isLeapingToGrab = false;
        }

        private float GetCurrentHorizontalSpeedLimit()
        {
            return maxSpeed;
        }

        private float GetCurrentSpeedFactorFromVelocity()
        {
            if (rb == null)
            {
                return 0f;
            }

            // On a slope the velocity is redirected along the surface, so the horizontal component
            // understates the real ground speed. Use the slope-projected ground speed there so
            // momentum (which powers jump/dash/damage) stays constant up/down gentle slopes.
            float speedNow = (isGrounded && _onSlope) ? Mathf.Abs(_lastGroundSpeed) : Mathf.Abs(rb.linearVelocity.x);
            // Normalize by character max speed, not current cap, to avoid false 1.0 factor while coasting.
            float referenceSpeed = Mathf.Max(0.01f, maxSpeed);
            return Mathf.Clamp01(speedNow / referenceSpeed);
        }

        private bool IsReversingDirection(float inputX, float currentSpeed)
        {
            if (Mathf.Abs(inputX) <= 0.1f)
            {
                return false;
            }

            if (Mathf.Abs(currentSpeed) <= reverseSpeedThreshold)
            {
                return false;
            }

            return Mathf.Sign(inputX) != Mathf.Sign(currentSpeed);
        }

        private void ApplyReverseBrake()
        {
            float brakeRate = isGrounded ? reverseGroundBrake : reverseAirBrake;
            float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, brakeRate * Time.deltaTime);
            rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
        }

        private void ApplyPostDashAirBrake()
        {
            if (rb == null || isGrounded)
            {
                return;
            }

            float maxAllowedSpeed = Mathf.Max(0.01f, maxSpeed * postDashMaxAirSpeedMultiplier);
            float currentSpeedX = rb.linearVelocity.x;

            if (Mathf.Abs(currentSpeedX) <= maxAllowedSpeed)
            {
                return;
            }

            float clampedTargetX = Mathf.Sign(currentSpeedX) * maxAllowedSpeed;
            float newSpeedX = Mathf.MoveTowards(currentSpeedX, clampedTargetX, postDashAirBrakeStrength * Time.deltaTime);
            rb.linearVelocity = new Vector2(newSpeedX, rb.linearVelocity.y);
        }

        // A shallow/diagonal air dash leaves a little upward carry at dash-end that reads as an
        // unwanted "pop". Cancel that residual upward velocity unless the player is deliberately
        // steering upward (a real up-dash). Direction is resolved via UpSign so it stays correct
        // regardless of the project's vertical-sign convention. Reuses wallInputThreshold as the
        // up-intent deadzone so no extra serialized knob is needed.
        private void CancelUnwantedPostDashUpwardCarry()
        {
            if (rb == null)
            {
                return;
            }

            bool steeringUpward = moveInput.y * UpSign > wallInputThreshold;
            bool carryingUpward = rb.linearVelocity.y * UpSign > 0f;

            if (carryingUpward && !steeringUpward)
            {
                // TEMP DIAGNOSTIC
                if (rb.linearVelocity.y > 5f) Debug.Log($"[CancelUpwardCarry] killing upward vel {rb.linearVelocity.y:F1}");
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }


        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Ground CircleCast probe (matches CheckGroundStatus).
            Collider2D bodyCol = _bodyCollider != null ? _bodyCollider : GetComponent<Collider2D>();
            if (bodyCol != null)
            {
                Bounds b = bodyCol.bounds;
                float radius = Mathf.Max(0.05f, b.extents.x - groundCheckSkin);
                Vector3 castStart = new Vector3(b.center.x, b.center.y, transform.position.z);
                Vector3 castEnd = castStart + Vector3.down * Mathf.Max(0.01f, b.extents.y - radius + groundSnapDistance);

                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(castStart, radius);
                Gizmos.DrawWireSphere(castEnd, radius);

                if (isGrounded)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(_groundPoint, _groundPoint + _groundNormal); // surface normal
                }
            }

            Vector3 worldCenter = transform.position;
            float offsetX = Mathf.Abs(wallCheckOffset.x);
            Vector3 leftCheckPos = worldCenter + new Vector3(-offsetX, wallCheckOffset.y, 0f);
            Vector3 rightCheckPos = worldCenter + new Vector3(offsetX, wallCheckOffset.y, 0f);

            Gizmos.color = isTouchingWall ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(leftCheckPos, wallCheckSize);
            Gizmos.DrawWireCube(rightCheckPos, wallCheckSize);

            Gizmos.color = isGrabbing ? Color.cyan : new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(worldCenter, grabCheckRadius);
        }

        #endregion
    }
}