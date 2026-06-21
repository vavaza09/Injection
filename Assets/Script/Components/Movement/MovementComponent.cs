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
        [FormerlySerializedAs("wallSlideMaxFallAtMaxMomentumMultiplier")]
        [SerializeField] private float wallSlideMaxFallAtMaxSpeedMultiplier = 1.2f;

        [Header("Dash Settings")]
        [SerializeField] private DashSettings dashSettings = new DashSettings();

        [Header("Ground Check")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask oneWayPlatformMask;
        [SerializeField] private float dropThroughSafetyTime = 0.75f;

        [Header("Knockback Settings")]
        [Tooltip("Seconds after a knockback during which move input and braking are ignored so the knockback velocity carries.")]
        [SerializeField] private float knockbackLockTime = 0.2f;

        [Header("Wall Jump Settings")]
        [SerializeField] private float wallJumpHorizontalSpeed = 80f;
        [SerializeField] private float wallJumpVerticalSpeed = -105f;
        [Tooltip("Wall jump launch (both axes) scales from 1x at zero approach speed up to this at full wall-entry momentum, matching the speed-powers-actions pillar.")]
        [SerializeField] private float wallJumpAtMaxSpeedMultiplier = 1.2f;
        [Tooltip("Seconds after a wall jump during which wall contact is ignored so the launch carries the player away.")]
        [SerializeField] private float wallJumpLockTime = 0.15f;

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
        private float _wallEntrySpeedFactor;
        private float _wallJumpLockTimer;
        private float _knockbackLockTimer;

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
        private static readonly Collider2D[] _groundHitBuffer = new Collider2D[8];
        private ContactFilter2D _groundFilter;

        // Grab state
        private bool isGrabbing;
        private bool isCaptured;
        private float _stunTimer;
        private SwingPoint currentSwingPoint;
        private float grabbedSpeedFactor;
        private float _autoGrabCooldownTimer;

        #endregion

        #region Properties

        /// <summary>Raised the instant a ground jump launches. Use for one-shot feedback (sfx/vfx).</summary>
        public event Action Jumped;
        /// <summary>Raised the instant a wall jump launches. Use for one-shot feedback (sfx/vfx).</summary>
        public event Action WallJumped;
        /// <summary>Raised whenever a grab succeeds (manual or auto). Use for one-shot feedback (sfx/slow-motion).</summary>
        public event Action GrabStarted;

        public bool IsDashing => _dashHandler != null && _dashHandler.IsDashing;
        public bool DashAttacking => _dashHandler != null && _dashHandler.DashAttacking;
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

            if (_logger == null)
            {
                Debug.LogWarning("[MovementComponent] Logger not injected, using Debug.Log");
            }
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
            _wallEntrySpeedFactor = 0f;
            _wallJumpLockTimer = 0f;
            _knockbackLockTimer = 0f;
            _postDashAirBrakeTimer = 0f;
            _wasDashingLastFrame = false;
            isGrabbing = false;
            isCaptured = false;
            _stunTimer = 0f;
            currentSwingPoint = null;
            grabbedSpeedFactor = 0f;

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
            if (_wallJumpLockTimer > 0f)
            {
                _wallJumpLockTimer = Mathf.Max(0f, _wallJumpLockTimer - Time.deltaTime);
            }

            if (_knockbackLockTimer > 0f)
            {
                _knockbackLockTimer = Mathf.Max(0f, _knockbackLockTimer - Time.deltaTime);
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
                ResetWallSlideState();
                return;
            }

            if (_autoGrabCooldownTimer > 0f)
                _autoGrabCooldownTimer -= Time.deltaTime;

            if (autoGrab && _autoGrabCooldownTimer <= 0f && !isGrabbing && !isGrounded && CanGrab)
            {
                TryStartGrab();
            }

            if (isGrabbing)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (isCaptured)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer < 0f) _stunTimer = 0f;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            UpdateJumpState();
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
            if (_knockbackLockTimer > 0f) return;
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

            _logger?.Log($"Bounced! Speed: {bounceSpeed}");
        }

        public void StartJumpGraceTime()
        {
            jumpGraceTimer = jumpGraceTime;
        }

        private void PerformWallJump()
        {
            int jumpAwayDirection = wallSideSign != 0
                ? -wallSideSign
                : (characterTransform != null && characterTransform.localScale.x >= 0 ? -1 : 1);

            // Scale the launch by the speed the player carried INTO the wall, not their
            // current velocity (wall-stick has already braked horizontal speed to ~0).
            // This keeps wall jumps on the "speed powers actions" pillar: a fast approach
            // launches harder. _wallEntrySpeedFactor is still valid here (the wall-jump
            // lock that zeroes it only takes effect on the next CheckWallStatus).
            float wallJumpMultiplier = Mathf.Lerp(1f, wallJumpAtMaxSpeedMultiplier, _wallEntrySpeedFactor);

            ResetWallSlideState();

            BeginJumpWindow();

            // Wall jump always launches in the "up" direction (UpSign, derived from jumpSpeed),
            // regardless of the serialized field's sign — a stale/negative value must never
            // fire the player into the ground.
            rb.linearVelocity = new Vector2(
                jumpAwayDirection * wallJumpHorizontalSpeed * wallJumpMultiplier,
                Mathf.Abs(wallJumpVerticalSpeed) * UpSign * wallJumpMultiplier
            );

            ApplyJumpStateAfterLaunch(rb.linearVelocity.y);

            _wallJumpLockTimer = wallJumpLockTime;
            WallJumped?.Invoke();
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

            if (groundCheck != null)
            {
                int hitCount = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, _groundFilter, _groundHitBuffer);
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hit = _groundHitBuffer[i];
                    if (_bodyCollider != null && Physics2D.GetIgnoreCollision(_bodyCollider, hit))
                        continue;

                    bool isOneWay = ((1 << hit.gameObject.layer) & oneWayPlatformMask.value) != 0;
                    // One-way platform: only counts as ground when feet are at/above the surface.
                    // If feet are below the top, the player is passing through from below — skip.
                    if (isOneWay && _bodyCollider != null && _bodyCollider.bounds.min.y < hit.bounds.max.y - 0.02f)
                        continue;

                    isGrounded = true;

                    if (_currentGroundCollider == null && isOneWay)
                        _currentGroundCollider = hit;
                }
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
                _wallEntrySpeedFactor = 0f;
                return;
            }

            Vector2 center = characterTransform.position;
            float offsetX = Mathf.Abs(wallCheckOffset.x);

            Vector2 leftCheckPos = center + new Vector2(-offsetX, wallCheckOffset.y);
            Vector2 rightCheckPos = center + new Vector2(offsetX, wallCheckOffset.y);

            bool leftHit = Physics2D.OverlapBox(leftCheckPos, wallCheckSize, 0f, climbableLayer);
            bool rightHit = Physics2D.OverlapBox(rightCheckPos, wallCheckSize, 0f, climbableLayer);

            bool wasTouchingWall = isTouchingWall;
            isTouchingWall = !isGrounded && (leftHit || rightHit);

            if (isTouchingWall && !wasTouchingWall)
                _wallEntrySpeedFactor = GetCurrentSpeedFactorFromVelocity();
            else if (!isTouchingWall)
                _wallEntrySpeedFactor = 0f;

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
            float maxSlideSpeed = wallSlideMaxFallSpeed * Mathf.Lerp(1f, wallSlideMaxFallAtMaxSpeedMultiplier, _wallEntrySpeedFactor);
            float currentGravityAlignedSpeed = rb.linearVelocity.y * gravityDirection;

            if (!_wasWallSliding)
            {
                _currentWallSlideSpeed = Mathf.Max(Mathf.Max(0f, currentGravityAlignedSpeed), _wallEntrySpeedFactor * maxSpeed);
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

            // Snapshot arrival speed BEFORE zeroing velocity — this is the launch-power source
            grabbedSpeedFactor = GetCurrentSpeedFactorFromVelocity();

            currentSwingPoint = target;
            isGrabbing = true;
            GrabStarted?.Invoke();

            // Pin to anchor
            rb.position = target.AnchorPosition;
            rb.linearVelocity = Vector2.zero;

            // Clear conflicting states
            ResetWallSlideState();
            isJumping = false;
            isFalling = false;
            jumpGraceTimer = 0f;
            varJumpTimer = 0f;
            autoJump = false;

            _logger?.Log($"Grab started on {target.name}, speedFactor={grabbedSpeedFactor:F2}");
            return true;
        }

        public void LaunchFromGrab(Vector2 aimDir)
        {
            if (rb == null || !isGrabbing) return;

            float speed = grabLaunchBaseSpeed * Mathf.Lerp(grabLaunchMinMultiplier, grabLaunchMaxMultiplier, grabbedSpeedFactor);
            rb.linearVelocity = aimDir.normalized * speed;

            currentSwingPoint = null;
            isGrabbing = false;
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
                _wallEntrySpeedFactor = 0f;
                _postDashAirBrakeTimer = 0f;
                _wasDashingLastFrame = false;
            }
        }

        public bool IsGrounded()
        {
            return isGrounded;
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

            float dir = Mathf.Sign(characterTransform.position.x - sourcePosition.x);
            if (dir == 0f)
                dir = characterTransform.localScale.x >= 0f ? 1f : -1f;

            Vector2 kick = new Vector2(dir, UpSign * upwardBias).normalized * force;

            ResetWallSlideState();
            isGrabbing = false;
            rb.linearVelocity = kick;
            _knockbackLockTimer = knockbackLockTime;
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

            float speedNow = Mathf.Abs(rb.linearVelocity.x);
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
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }


        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
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