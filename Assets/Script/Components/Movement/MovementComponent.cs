using ILogger = Core.Logging.ILogger;
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
        [SerializeField] private float acceleration = 48f;
        [SerializeField] private float deceleration = 44f;

        [Header("Direction Change Physics")]
        [SerializeField] private float reverseGroundBrake = 120f;
        [SerializeField] private float reverseAirBrake = 70f;
        [SerializeField, Range(0f, 1f)] private float reverseMomentumLoss = 0.2f;
        [SerializeField] private float reverseMomentumDecayMultiplier = 2.5f;
        [SerializeField] private float reverseSpeedThreshold = 0.25f;

        [Header("Momentum Settings")]
        [SerializeField, Range(0.1f, 1f)] private float minSpeedMultiplier = 0.6f;
        [SerializeField] private float momentumBuildRate = 2.1f;
        [SerializeField] private float momentumDecayRate = 1.4f;
        [SerializeField] private float momentumLossOnHit = 0.3f;
        [SerializeField] private float momentumLossOnWallCrash = 0.2f;
        [SerializeField] private float climbMomentumBoostMultiplier = 1.2f;
        [SerializeField] private float dashMomentumMultiplier = 1.5f;
        [SerializeField] private float wallSlideMaxFallAtMaxMomentumMultiplier = 1.2f;

        [Header("Speed Based Action Boost")]
        [SerializeField] private float jumpAtMaxSpeedMultiplier = 1.2f;
        [SerializeField] private float dashAtMaxSpeedMultiplier = 1.45f;

        [Header("Air Control")]
        [SerializeField] private float airAcceleration = 30f;
        [SerializeField] private float airDeceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.8f;

        [Header("Post Dash Air Control")]
        [SerializeField] private float postDashAirControlTime = 0.2f;
        [SerializeField] private float postDashAirAccelerationOverride = 45f;
        [SerializeField, Range(0.1f, 2f)] private float postDashAirControlBoost = 1.15f;
        [SerializeField] private float postDashAirBrakeTime = 0.25f;
        [SerializeField] private float postDashAirBrakeStrength = 120f;
        [SerializeField, Range(0.5f, 2f)] private float postDashMaxAirSpeedMultiplier = 1.15f;
        [SerializeField] private bool suppressUpwardCarryWhenRunHeldAfterDash = true;
        [SerializeField, Range(0f, 1f)] private float postDashRunHoldThreshold = 0.1f;

        [Header("Jump Settings (Celeste-inspired)")]
        [SerializeField] private float jumpSpeed = -105f;
        [SerializeField] private float jumpHBoost = 40f;
        [SerializeField] private float varJumpTime = 0.2f;
        [SerializeField] private float jumpGraceTime = 0.1f;

        [Header("Gravity Settings")]
        [SerializeField] private float gravity = 900f;
        [SerializeField] private float maxFall = 160f;
        [SerializeField] private float halfGravThreshold = 40f;

        [Header("Advanced Jump")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float lowJumpGravityMultiplier = 3f;
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

        [Header("Climb Settings")]
        [SerializeField] private float climbSpeed = 8f;
        [SerializeField] private float wallJumpHorizontalSpeed = 80f;
        [SerializeField] private float wallJumpVerticalSpeed = -105f;
        [SerializeField] private float climbInputThreshold = 0.1f;

        [Header("Wall Check")]
        [SerializeField] private Vector2 wallCheckSize = new Vector2(0.2f, 0.8f);
        [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.35f, 0f);
        [SerializeField] private LayerMask climbableLayer;

        [Header("Grab Settings")]
        [SerializeField] private LayerMask grabbableLayer;
        [SerializeField] private float grabCheckRadius = 1.5f;
        [SerializeField] private float grabLaunchBaseSpeed = 200f;
        [SerializeField, Range(0.5f, 1f)] private float grabLaunchMinMultiplier = 0.6f;
        [SerializeField, Range(1f, 2f)] private float grabLaunchMaxMultiplier = 1.4f;

        [Header("Debug")]
        [SerializeField] private bool enableVerboseLogs = false;

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
        private bool isClimbing;
        private int wallSideSign;
        private bool _wasWallSliding;
        private float _currentWallSlideSpeed;
        private bool _wasReversingDirection;
        private Vector2 moveInput;
        private float _momentumNormalized;
        private float _currentMoveSpeed;

        // Dash (composed, not inherited)
        private DashHandler _dashHandler;
        private Coroutine _dashCoroutine;
        private float _postDashAirControlTimer;
        private float _postDashAirBrakeTimer;
        private bool _wasDashingLastFrame;

        // Grab state
        private bool isGrabbing;
        private SwingPoint currentSwingPoint;
        private float grabbedSpeedFactor;

        #endregion

        #region Properties

        public bool IsDashing => _dashHandler != null && _dashHandler.IsDashing;
        public bool DashAttacking => _dashHandler != null && _dashHandler.DashAttacking;
        public bool CanDash => _dashHandler != null && _dashHandler.CanDash;
        public int CurrentDashes => _dashHandler?.CurrentDashes ?? 0;
        public int MaxDashes => _dashHandler?.MaxDashes ?? 0;
        public bool IsClimbingState => isClimbing;
        public float MomentumNormalized => _momentumNormalized;
        public float CurrentMoveSpeed => _currentMoveSpeed;
        public float MaxSpeed => maxSpeed;
        public float MovementAttackMultiplier => Mathf.Lerp(1f, dashMomentumMultiplier, _momentumNormalized);

        public bool IsGrabbing => isGrabbing;
        public bool CanGrab => !isGrabbing && FindGrabTarget() != null;

        public bool AutoJump
        {
            get => autoJump;
            set => autoJump = value;
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
            isClimbing = false;
            isTouchingWall = false;
            wallSideSign = 0;
            _wasWallSliding = false;
            _currentWallSlideSpeed = 0f;
            _wasReversingDirection = false;
            moveInput = Vector2.zero;
            _momentumNormalized = 0f;
            _currentMoveSpeed = 0f;
            _postDashAirControlTimer = 0f;
            _postDashAirBrakeTimer = 0f;
            _wasDashingLastFrame = false;
            isGrabbing = false;
            currentSwingPoint = null;
            grabbedSpeedFactor = 0f;

            if (rb != null)
            {
                if (rb.gravityScale != 0)
                {
                    Debug.LogWarning($"[MovementComponent] Rigidbody2D.gravityScale was {rb.gravityScale}, forcing to 0");
                    rb.gravityScale = 0;
                }

                if (enableVerboseLogs)
                {
                    Debug.Log($"✅ Rigidbody2D Settings:");
                    Debug.Log($"   - Gravity Scale: {rb.gravityScale}");
                    Debug.Log($"   - Body Type: {rb.bodyType}");
                    Debug.Log($"   - Mass: {rb.mass}");
                }
            }

            // Initialize dash handler (composition)
            _dashHandler = new DashHandler(dashSettings);
            _dashHandler.Initialize(rb);

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
                if (enableVerboseLogs)
                {
                    Debug.Log($"[MovementComponent] Auto-created GroundCheck for {characterTransform.name}");
                }
            }
        }

        #endregion

        #region Update

        /// <summary>
        /// Call this in character's Update
        /// </summary>
        public void UpdateMovement()
        {
            CheckGroundStatus();
            CheckWallStatus();
            UpdateMomentum();
            
            if (_dashHandler != null)
            {
                _dashHandler.UpdateTimers();

                if (isGrounded)
                {
                    _dashHandler.RefillDash();
                }
            }

            bool isDashingNow = IsDashing;
            if (_wasDashingLastFrame && !isDashingNow && !isGrounded)
            {
                _postDashAirControlTimer = postDashAirControlTime;
                _postDashAirBrakeTimer = postDashAirBrakeTime;
                SuppressUnwantedUpwardCarryAfterDash();
            }

            _wasDashingLastFrame = isDashingNow;

            if (_postDashAirControlTimer > 0f)
            {
                _postDashAirControlTimer = Mathf.Max(0f, _postDashAirControlTimer - Time.deltaTime);
            }

            if (_postDashAirBrakeTimer > 0f)
            {
                _postDashAirBrakeTimer = Mathf.Max(0f, _postDashAirBrakeTimer - Time.deltaTime);
            }

            if (isDashingNow)
            {
                StopClimb();
                ResetWallSlideState();
                return;
            }

            if (isGrabbing)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            UpdateClimbState();
            if (isClimbing) return;

            UpdateJumpState();
        }

        #endregion

        #region Horizontal Movement

        public void Move(Vector2 direction)
        {
            moveInput = direction;

            if (IsDashing) return;
            if (isGrabbing) return;
            if (!canMove || rb == null) return;

            if (isClimbing)
            {
                float climbVelocity = Mathf.Abs(direction.y) >= climbInputThreshold
                    ? direction.y * GetCurrentClimbSpeed()
                    : 0f;
                rb.linearVelocity = new Vector2(0f, climbVelocity);
                return;
            }

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

                if (!_wasReversingDirection)
                {
                    ReduceMomentum(reverseMomentumLoss);
                }

                _wasReversingDirection = true;
                return;
            }

            _wasReversingDirection = false;

            float effectiveMoveSpeed = GetCurrentHorizontalSpeedLimit();
            float targetSpeed = direction.x * effectiveMoveSpeed;
            bool isPushingIntoWall = !isGrounded
                                     && isTouchingWall
                                     && !isClimbing
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

                if (_postDashAirControlTimer > 0f)
                {
                    accelRate = Mathf.Max(accelRate, postDashAirAccelerationOverride);

                    // Rebuild target speed from the horizontal intent so control still works
                    // even if scene overrides set air control multipliers near zero.
                    if (Mathf.Abs(direction.x) > 0.01f)
                    {
                        float assistedTargetSpeed = direction.x * effectiveMoveSpeed * postDashAirControlBoost;
                        if (Mathf.Abs(assistedTargetSpeed) > Mathf.Abs(targetSpeed))
                        {
                            targetSpeed = assistedTargetSpeed;
                        }
                    }
                }

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
            if (enableVerboseLogs)
            {
                Debug.Log($"🎯 Jump() START - Current velocity: {rb?.linearVelocity}");
            }

            if (!CanExecuteJump())
            {
                return;
            }

            if (isClimbing)
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

            StopClimb();

            BeginJumpWindow(varJumpTime);

            rb.linearVelocity = new Vector2(
                jumpAwayDirection * wallJumpHorizontalSpeed,
                wallJumpVerticalSpeed
            );

            ApplyJumpStateAfterLaunch(rb.linearVelocity.y);
        }

        private bool CanExecuteJump()
        {
            if (!canMove || rb == null)
            {
                if (enableVerboseLogs)
                {
                    Debug.LogWarning($"❌ Jump BLOCKED! canMove: {canMove}, rb: {rb != null}");
                }
                return false;
            }

            if (isClimbing)
            {
                return true;
            }

            if (jumpGraceTimer <= 0f && !isGrounded)
            {
                if (enableVerboseLogs)
                {
                    Debug.LogWarning($"❌ Jump BLOCKED! jumpGraceTimer: {jumpGraceTimer:F3}, isGrounded: {isGrounded}");
                }
                return false;
            }

            return true;
        }

        private void ExecuteGroundJump()
        {
            if (enableVerboseLogs)
            {
                Debug.Log($"✅ Jump SUCCESS! Applying velocity...");
            }

            BeginJumpWindow(varJumpTime);

            float currentSpeedX = rb.linearVelocity.x;
            float moveDirection = ResolveJumpDirection(currentSpeedX);
            float speedFactor = GetCurrentSpeedFactorFromVelocity();
            float jumpBoostAmount = CalculateJumpBoostAmount(currentSpeedX, moveDirection, speedFactor);
            float jumpVerticalSpeed = jumpSpeed * Mathf.Lerp(1f, jumpAtMaxSpeedMultiplier, speedFactor);

            Vector2 jumpVelocity = new Vector2(currentSpeedX + jumpBoostAmount, jumpVerticalSpeed);
            rb.linearVelocity = jumpVelocity;
            ApplyJumpStateAfterLaunch(jumpVelocity.y);

            if (enableVerboseLogs)
            {
                Debug.Log($"   - Current Speed X: {currentSpeedX:F2}");
                Debug.Log($"   - Speed Factor: {speedFactor:F2}");
                Debug.Log($"   - Jump Boost: {jumpBoostAmount:F2}");
                Debug.Log($"   - Jump Vertical Speed: {jumpVerticalSpeed:F2}");
                Debug.Log($"   - New Velocity: {jumpVelocity}");
                Debug.Log($"✅ Jump applied! Final velocity: {rb.linearVelocity}");
                Debug.Log($"   - varJumpSpeed: {varJumpSpeed}");
                Debug.Log($"   - autoJump: {autoJump}");
            }
        }

        private void BeginJumpWindow(float duration)
        {
            jumpGraceTimer = 0f;
            varJumpTimer = duration;
            autoJump = true;
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

            if (HandleClimbJumpState()) return;
            if (HandleWallSlideJumpState()) return;

            ResetWallSlideState();

            if (HandleGroundedJumpState()) return;

            float deltaTime = Time.deltaTime;
            UpdateJumpTimers(deltaTime);
            ApplyVariableJumpHold(deltaTime);
            ApplyJumpGravity(deltaTime);
            UpdateAirborneJumpFlags();
        }

        private bool HandleClimbJumpState()
        {
            if (!isClimbing)
            {
                return false;
            }

            ResetWallSlideState();
            isJumping = false;
            isFalling = false;
            return true;
        }

        private bool HandleWallSlideJumpState()
        {
            bool wallSlideActive = isTouchingWall && !isGrounded;
            if (!wallSlideActive)
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
            if (Mathf.Abs(verticalSpeed) < halfGravThreshold && (autoJump || varJumpTimer > 0f))
            {
                return 0.5f;
            }

            if (verticalSpeed > 0f)
            {
                return fallGravityMultiplier;
            }

            if (verticalSpeed < 0f && varJumpTimer <= 0f)
            {
                return lowJumpGravityMultiplier;
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

            if (groundCheck != null)
            {
                isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
            }

            if (previousGrounded != isGrounded)
            {
                if (enableVerboseLogs)
                {
                    Debug.Log($"Ground State Changed: {previousGrounded} -> {isGrounded}");
                }
            }

            if (previousGrounded && !isGrounded && rb != null && rb.linearVelocity.y >= 0)
            {
                StartJumpGraceTime();
                if (enableVerboseLogs)
                {
                    Debug.Log("Started Jump Grace Time (Coyote Time)");
                }
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

            Vector2 center = characterTransform.position;
            float offsetX = Mathf.Abs(wallCheckOffset.x);

            Vector2 leftCheckPos = center + new Vector2(-offsetX, wallCheckOffset.y);
            Vector2 rightCheckPos = center + new Vector2(offsetX, wallCheckOffset.y);

            bool leftHit = Physics2D.OverlapBox(leftCheckPos, wallCheckSize, 0f, climbableLayer);
            bool rightHit = Physics2D.OverlapBox(rightCheckPos, wallCheckSize, 0f, climbableLayer);

            isTouchingWall = !isGrounded && (leftHit || rightHit);
            if (enableVerboseLogs)
            {
                Debug.Log($"Wall Check - Left: {leftHit}, Right: {rightHit}, IsTouchingWall: {isTouchingWall}");
            }
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

        private void UpdateClimbState()
        {
            if (!canMove || rb == null)
            {
                StopClimb();
                return;
            }

            bool wantsToClimb = Mathf.Abs(moveInput.y) >= climbInputThreshold;
            bool canClimbNow = !isGrounded && isTouchingWall;

            if (canClimbNow && wantsToClimb)
            {
                if (!isClimbing)
                {
                    isClimbing = true;
                    isJumping = false;
                    isFalling = false;
                    jumpGraceTimer = 0;
                    varJumpTimer = 0;
                    autoJump = false;
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            StopClimb();
        }

        private void StopClimb()
        {
            if (!isClimbing) return;
            isClimbing = false;
        }

        private void UpdateWallSlideVelocity()
        {
            if (rb == null)
            {
                return;
            }

            float gravityDirection = gravity >= 0f ? -1f : 1f;
            float maxSlideSpeed = wallSlideMaxFallSpeed * Mathf.Lerp(1f, wallSlideMaxFallAtMaxMomentumMultiplier, _momentumNormalized);
            float currentGravityAlignedSpeed = rb.linearVelocity.y * gravityDirection;

            if (!_wasWallSliding)
            {
                // Use the same movement speed source as Player/UI instead of a separate wall-slide start value.
                _currentWallSlideSpeed = Mathf.Max(Mathf.Max(0f, currentGravityAlignedSpeed), _currentMoveSpeed);
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

            // Pin to anchor
            rb.position = target.AnchorPosition;
            rb.linearVelocity = Vector2.zero;

            // Clear conflicting states
            StopClimb();
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

            _logger?.Log("Grab released (drop)");
        }

        #endregion

        #region Utility

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
            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                RecalculateCurrentMoveSpeed();
            }
        }

        public void ReduceMomentum(float amount)
        {
            _momentumNormalized = Mathf.Clamp01(_momentumNormalized - Mathf.Abs(amount));
            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                RecalculateCurrentMoveSpeed();
            }
        }

        public void SetCanMove(bool value)
        {
            canMove = value;
            if (!canMove)
            {
                StopClimb();
                _momentumNormalized = 0f;
                _currentMoveSpeed = 0f;
                _wasReversingDirection = false;
                _postDashAirControlTimer = 0f;
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

        public bool IsClimbing()
        {
            return isClimbing;
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
            ReduceMomentum(momentumLossOnWallCrash);
        }

        public void NotifyDamageTaken()
        {
            ReduceMomentum(momentumLossOnHit);
        }

        private void UpdateMomentum()
        {
            if (!canMove)
            {
                _momentumNormalized = 0f;
                _currentMoveSpeed = 0f;
                return;
            }

            if (isGrabbing) return;

            bool hasHorizontalInput = Mathf.Abs(moveInput.x) > 0.1f;
            bool isReversingDirection = rb != null && IsReversingDirection(moveInput.x, rb.linearVelocity.x);
            bool isBuildingMomentum = isGrounded && !IsDashing && hasHorizontalInput && !isReversingDirection;

            if (isBuildingMomentum)
            {
                _momentumNormalized += momentumBuildRate * Time.deltaTime;
            }
            else
            {
                float decayMultiplier = isReversingDirection ? reverseMomentumDecayMultiplier : 1f;
                _momentumNormalized -= momentumDecayRate * decayMultiplier * Time.deltaTime;
            }

            _momentumNormalized = Mathf.Clamp01(_momentumNormalized);

            if (hasHorizontalInput)
            {
                RecalculateCurrentMoveSpeed();
                return;
            }

            // No move input: follow real horizontal speed so UI and player motion stay in sync.
            if (rb != null)
            {
                _currentMoveSpeed = Mathf.Abs(rb.linearVelocity.x);
                return;
            }

            float speedDecayRate = isGrounded ? deceleration : airDeceleration;
            _currentMoveSpeed = Mathf.MoveTowards(_currentMoveSpeed, 0f, speedDecayRate * Time.deltaTime);
        }

        private float GetCurrentHorizontalSpeedLimit()
        {
            float speedLimit = _currentMoveSpeed;
            if (Mathf.Abs(moveInput.x) > 0.1f && speedLimit <= 0f)
            {
                speedLimit = maxSpeed * minSpeedMultiplier;
            }

            return Mathf.Max(0.01f, speedLimit);
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

        private float GetCurrentClimbSpeed()
        {
            return climbSpeed * Mathf.Lerp(1f, climbMomentumBoostMultiplier, _momentumNormalized);
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

        private void SuppressUnwantedUpwardCarryAfterDash()
        {
            if (!suppressUpwardCarryWhenRunHeldAfterDash || rb == null)
            {
                return;
            }

            bool isHoldingRun = Mathf.Abs(moveInput.x) >= postDashRunHoldThreshold;
            bool hasVerticalIntent = Mathf.Abs(moveInput.y) >= climbInputThreshold;

            if (!isHoldingRun || hasVerticalIntent)
            {
                return;
            }

            if (rb.linearVelocity.y <= 0f)
            {
                return;
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }

        private void RecalculateCurrentMoveSpeed()
        {
            if (!canMove)
            {
                _currentMoveSpeed = 0f;
                return;
            }

            _currentMoveSpeed = Mathf.Lerp(maxSpeed * minSpeedMultiplier, maxSpeed, _momentumNormalized);
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