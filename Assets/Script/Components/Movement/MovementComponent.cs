using ILogger = Core.Logging.ILogger;
using UnityEngine;
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
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float deceleration = 50f;

        [Header("Air Control")]
        [SerializeField] private float airAcceleration = 30f;
        [SerializeField] private float airDeceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.8f;

        [Header("Jump Settings (Celeste-inspired)")]
        [SerializeField] private float jumpSpeed = -105f;
        [SerializeField] private float jumpHBoost = 40f;
        [SerializeField] private float varJumpTime = 0.2f;
        [SerializeField] private float jumpGraceTime = 0.1f;

        [Header("Gravity Settings")]
        [SerializeField] private float gravity = 900f;
        [SerializeField] private float maxFall = 160f;
        [SerializeField] private float fastMaxFall = 240f;
        [SerializeField] private float halfGravThreshold = 40f;

        [Header("Advanced Jump")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float lowJumpGravityMultiplier = 3f;
        [SerializeField] private float fallAirControlMultiplier = 0.75f;

        [Header("Wall Stick")]
        [SerializeField] private float wallStickGravityMultiplier = 0.35f;
        [SerializeField] private float wallSlideMaxFallSpeed = 90f;
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

        #endregion

        #region Private State

        // Jump State
        private bool isJumping;
        private float jumpTimer;
        private bool isFalling;
        private float varJumpSpeed;
        private float varJumpTimer;
        private float jumpGraceTimer;
        private bool autoJump;
        private float autoJumpTimer;
        private const float bounceAutoJumpTime = 0.1f;

        // Core References
        private Rigidbody2D rb;
        private Transform characterTransform;
        private Transform groundCheck;
        private bool isGrounded;
        private bool wasOnGround;
        [SerializeField] private bool canMove = true;
        private bool isTouchingWall;
        private bool isClimbing;
        private int wallSideSign;
        private Vector2 moveInput;

        // Dash (composed, not inherited)
        private DashHandler _dashHandler;
        private Coroutine _dashCoroutine;

        #endregion

        #region Properties

        public bool IsDashing => _dashHandler != null && _dashHandler.IsDashing;
        public bool DashAttacking => _dashHandler != null && _dashHandler.DashAttacking;
        public int CurrentDashes => _dashHandler?.CurrentDashes ?? 0;
        public int MaxDashes => _dashHandler?.MaxDashes ?? 0;
        public bool IsClimbingState => isClimbing;

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
            moveInput = Vector2.zero;
            

            if (rb != null)
            {
                if (rb.gravityScale != 0)
                {
                    Debug.LogWarning($"[MovementComponent] Rigidbody2D.gravityScale was {rb.gravityScale}, forcing to 0");
                    rb.gravityScale = 0;
                }

                Debug.Log($"✅ Rigidbody2D Settings:");
                Debug.Log($"   - Gravity Scale: {rb.gravityScale}");
                Debug.Log($"   - Body Type: {rb.bodyType}");
                Debug.Log($"   - Mass: {rb.mass}");
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
                Debug.Log($"[MovementComponent] Auto-created GroundCheck for {characterTransform.name}");
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
            
            if (_dashHandler != null)
            {
                _dashHandler.UpdateTimers();

                if (isGrounded)
                {
                    _dashHandler.RefillDash();
                }
            }

            if (IsDashing)
            {
                StopClimb();
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
            if (!canMove || rb == null) return;

            if (isClimbing)
            {
                float climbVelocity = Mathf.Abs(direction.y) >= climbInputThreshold
                    ? direction.y * climbSpeed
                    : 0f;
                rb.linearVelocity = new Vector2(0f, climbVelocity);
                return;
            }

            float targetSpeed = direction.x * moveSpeed;
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

            float currentSpeed = rb.linearVelocity.x;

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
            }

            float speedDiff = targetSpeed - currentSpeed;
            float movement = speedDiff * accelRate;

            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);

            if (Mathf.Abs(rb.linearVelocity.x) > moveSpeed)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * moveSpeed, rb.linearVelocity.y);
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
            if (_dashHandler == null || !_dashHandler.CanDash || direction == Vector2.zero) return;

            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
            }

            _dashCoroutine = StartCoroutine(DashCoroutineWrapper(direction));
        }

        private IEnumerator DashCoroutineWrapper(Vector2 direction)
        {
            yield return StartCoroutine(_dashHandler.DashCoroutine(direction, isGrounded));
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
            Debug.Log($"🎯 Jump() START - Current velocity: {rb?.linearVelocity}");

            if (!canMove || rb == null)
            {
                Debug.LogWarning($"❌ Jump BLOCKED! canMove: {canMove}, rb: {rb != null}");
                return;
            }

            if (isClimbing)
            {
                PerformWallJump();
                return;
            }

            if (jumpGraceTimer <= 0 && !isGrounded)
            {
                Debug.LogWarning($"❌ Jump BLOCKED! jumpGraceTimer: {jumpGraceTimer:F3}, isGrounded: {isGrounded}");
                return;
            }

            Debug.Log($"✅ Jump SUCCESS! Applying velocity...");

            jumpGraceTimer = 0;
            varJumpTimer = varJumpTime;
            autoJump = true;

            float currentSpeedX = rb.linearVelocity.x;
            float moveDirection = Mathf.Sign(currentSpeedX);

            float jumpBoostAmount = 0;
            if (Mathf.Abs(currentSpeedX) > 0.5f)
            {
                float maxBoost = Mathf.Min(jumpHBoost, moveSpeed - Mathf.Abs(currentSpeedX));
                jumpBoostAmount = maxBoost * moveDirection;
            }

            Vector2 newVelocity = new Vector2(
                currentSpeedX + jumpBoostAmount,
                jumpSpeed
            );

            Debug.Log($"   - Current Speed X: {currentSpeedX:F2}");
            Debug.Log($"   - Jump Boost: {jumpBoostAmount:F2}");
            Debug.Log($"   - New Velocity: {newVelocity}");

            rb.linearVelocity = newVelocity;

            varJumpSpeed = rb.linearVelocity.y;
            isJumping = true;
            jumpTimer = 0f;
            isFalling = false;

            Debug.Log($"✅ Jump applied! Final velocity: {rb.linearVelocity}");
            Debug.Log($"   - varJumpSpeed: {varJumpSpeed}");
            Debug.Log($"   - autoJump: {autoJump}");
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
            varJumpTimer = 0.2f;
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

            jumpGraceTimer = 0;
            varJumpTimer = varJumpTime;
            autoJump = true;

            rb.linearVelocity = new Vector2(
                jumpAwayDirection * wallJumpHorizontalSpeed,
                wallJumpVerticalSpeed
            );

            varJumpSpeed = rb.linearVelocity.y;
            isJumping = true;
            jumpTimer = 0f;
            isFalling = false;
        }

        #endregion

        #region Jump State

        private void UpdateJumpState()
        {
            if (rb == null) return;

            if (isClimbing)
            {
                isJumping = false;
                isFalling = false;
                return;
            }

            wasOnGround = isGrounded;

            if (isGrounded)
            {
                isJumping = false;
                isFalling = false;
                jumpTimer = 0f;
                jumpGraceTimer = jumpGraceTime;
                autoJump = false;
                return;
            }

            if (jumpGraceTimer > 0)
            {
                jumpGraceTimer -= Time.deltaTime;
            }

            if (autoJumpTimer > 0)
            {
                if (autoJump)
                {
                    autoJumpTimer -= Time.deltaTime;
                    if (autoJumpTimer <= 0)
                        autoJump = false;
                }
                else
                {
                    autoJumpTimer = 0;
                }
            }

            if (varJumpTimer > 0)
            {
                if (autoJump)
                {
                    rb.linearVelocity = new Vector2(
                        rb.linearVelocity.x,
                        Mathf.Min(rb.linearVelocity.y, varJumpSpeed)
                    );
                    varJumpTimer -= Time.deltaTime;
                }
                else
                {
                    varJumpTimer = 0;
                }
            }

            // Apply Gravity with Half Gravity Threshold
            if (!isGrounded)
            {
                float gravityMultiplier = 1f;
                bool isPushingIntoWall = wallSideSign != 0
                                         && Mathf.Abs(moveInput.x) >= wallInputThreshold
                                         && Mathf.Sign(moveInput.x) == wallSideSign;
                bool wallStickActive = isTouchingWall
                                       && !isClimbing;

                if (Mathf.Abs(rb.linearVelocity.y) < halfGravThreshold && (autoJump || varJumpTimer > 0))
                {
                    gravityMultiplier = 0.5f;
                }
                else if (rb.linearVelocity.y > 0)
                {
                    gravityMultiplier = fallGravityMultiplier;
                }
                else if (rb.linearVelocity.y < 0 && varJumpTimer <= 0)
                {
                    gravityMultiplier = lowJumpGravityMultiplier;
                }

                if (wallStickActive)
                {
                    gravityMultiplier *= wallStickGravityMultiplier;
                }

                float gravityForce = gravity * gravityMultiplier * Time.deltaTime;

                rb.linearVelocity = new Vector2(
                    rb.linearVelocity.x,
                    rb.linearVelocity.y - gravityForce
                );

                float maxFallSpeed = maxFall;
                if (wallStickActive)
                {
                    maxFallSpeed = Mathf.Min(maxFallSpeed, wallSlideMaxFallSpeed);
                }

                if (rb.linearVelocity.y > maxFallSpeed)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
                }
            }

            if (rb.linearVelocity.y > 0)
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
                Debug.Log($"Ground State Changed: {previousGrounded} → {isGrounded}");
            }

            if (previousGrounded && !isGrounded && rb != null && rb.linearVelocity.y >= 0)
            {
                StartJumpGraceTime();
                Debug.Log("🕐 Started Jump Grace Time (Coyote Time)");
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
            Debug.Log($"Wall Check - Left: {leftHit}, Right: {rightHit}, IsTouchingWall: {isTouchingWall}");
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
            moveSpeed = speed;
        }

        public void SetCanMove(bool value)
        {
            canMove = value;
            if (!canMove)
            {
                StopClimb();
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
        }

        #endregion
    }
}