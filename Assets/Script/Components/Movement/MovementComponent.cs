using ILogger = Core.Logging.ILogger;
using UnityEngine;
using Core.Logging;
using VContainer;

namespace Game.Components.Movement
{
    public class MovementComponent : MonoBehaviour
    {
        private ILogger _logger; 
        
        // Movement Settings
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float deceleration = 50f;

        // Air Control
        [SerializeField] private float airAcceleration = 30f;
        [SerializeField] private float airDeceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.8f;

        // Jump Settings
        [SerializeField] private float jumpInitialSpeed = 12f;
        [SerializeField] private float jumpHangTime = 0.25f;
        [SerializeField] private float fallTerminalVelocity = -100f;
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float lowJumpGravityMultiplier = 3f;
        [SerializeField] private float fallAirControlMultiplier = 0.75f;
        
        // Jump State
        private bool isJumping;
        private float jumpTimer;
        private bool isFalling;

        // Ground Check
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody2D rb;
        private Transform characterTransform;
        private Transform groundCheck;
        private bool isGrounded;
        [SerializeField] private bool canMove = true;

        // ⭐ ใช้ VContainer Inject แทน Constructor
        [Inject]
        public void Construct(LoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<MovementComponent>();
            _logger?.Log("MovementComponent injected via VContainer");
        }

        // ⭐ ใช้ Awake แทน Constructor
        private void Awake()
        {
            // ตั้งค่า LayerMask ถ้ายังไม่ได้ตั้ง
            if (groundLayer == 0)
            {
                groundLayer = LayerMask.GetMask("Ground");
            }

            // ถ้าไม่มี Logger (ไม่ได้ Inject)
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
            SetupGroundCheck();
            
            _logger?.Log($"MovementComponent initialized for {transform.name}");
        }

        /// <summary>
        /// Auto-create or find GroundCheck Transform
        /// </summary>
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

        /// <summary>
        /// Call this in character's Update or FixedUpdate
        /// </summary>
        public void Update()
        {
            CheckGroundStatus();
            UpdateJumpState();
        }

        public void Move(Vector2 direction)
        {
            if (!canMove || rb == null) return;

            float targetSpeed = direction.x * moveSpeed;
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

        public void Jump()
        {
            if (!isGrounded || !canMove || rb == null) return;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpInitialSpeed);
            isJumping = true;
            jumpTimer = 0f;
            isFalling = false;

            _logger?.Log("Jump started");
        }

        private void UpdateJumpState()
        {
            if (rb == null) return;

            if (isGrounded)
            {
                isJumping = false;
                isFalling = false;
                jumpTimer = 0f;
                return;
            }

            if (isJumping)
            {
                jumpTimer += Time.deltaTime;

                if (jumpTimer >= jumpHangTime && rb.linearVelocity.y <= 0)
                {
                    isJumping = false;
                    isFalling = true;
                }
            }

            if (rb.linearVelocity.y < 0)
            {
                isFalling = true;
                
                float extraGravity = Physics2D.gravity.y * (fallGravityMultiplier - 1) * Time.deltaTime;
                rb.linearVelocity += new Vector2(0, extraGravity);
                
                if (rb.linearVelocity.y < fallTerminalVelocity)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, fallTerminalVelocity);
                }
            }
            else if (rb.linearVelocity.y > 0 && !isJumping)
            {
                float extraGravity = Physics2D.gravity.y * (lowJumpGravityMultiplier - 1) * Time.deltaTime;
                rb.linearVelocity += new Vector2(0, extraGravity);
            }
        }

        public void CancelJump()
        {
            if (isJumping && rb != null && rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
                isJumping = false;
                isFalling = true;
            }
        }

        public void Stop()
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }

        public void SetSpeed(float speed)
        {
            moveSpeed = speed;
        }

        public void SetCanMove(bool value)
        {
            canMove = value;
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

        public void SetJumpSettings(float initialSpeed, float hangTime, float terminalVelocity, float fallControl)
        {
            jumpInitialSpeed = initialSpeed;
            jumpHangTime = hangTime;
            fallTerminalVelocity = terminalVelocity;
            fallAirControlMultiplier = fallControl;
        }

        public void SetGravityMultipliers(float fallMultiplier, float lowJumpMultiplier)
        {
            fallGravityMultiplier = fallMultiplier;
            lowJumpGravityMultiplier = lowJumpMultiplier;
        }

        private void CheckGroundStatus()
        {
            if (groundCheck != null)
            {
                isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
            }
        }

        public void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }
}