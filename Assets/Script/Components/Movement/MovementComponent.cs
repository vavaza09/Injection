using ILogger = Core.Logging.ILogger;
using UnityEngine;
using Core.Logging;

namespace Game.Components.Movement
{
    public class MovementComponent
    {
        private readonly ILogger _logger;
        // Movement Settings
        private float moveSpeed = 8f;
        private float acceleration = 40f;
        private float deceleration = 50f;

        // Air Control
        private float airAcceleration = 30f;
        private float airDeceleration = 30f;
        private float airControlMultiplier = 0.8f;

        // Ground Check
        private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
        private LayerMask groundLayer;

        private Rigidbody2D rb;
        private Transform characterTransform;
        private Transform groundCheck;
        private bool isGrounded;
        private bool canMove = true;

        // Constructor
        public MovementComponent(LoggerFactory loggerFactory)
        {
            groundLayer = LayerMask.GetMask("Ground");
            _logger = loggerFactory.CreateLogger<MovementComponent>();
            _logger.Log("MovementComponent created");
        }

        /// <summary>
        /// Initialize component with required dependencies
        /// </summary>
        public void Initialize(Rigidbody2D rigidbody, Transform transform)
        {
            rb = rigidbody;
            characterTransform = transform;
            SetupGroundCheck();
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
        }

        public void Move(Vector2 direction)
        {
            if (!canMove || rb == null) return;

            float targetSpeed = direction.x * moveSpeed;
            float currentSpeed = rb.linearVelocity.x;

            float accelRate;
            if (isGrounded)
            {
                accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
            }
            else
            {
                accelRate = Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration;
                targetSpeed *= airControlMultiplier;
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

        private void CheckGroundStatus()
        {
            if (groundCheck != null)
            {
                isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
            }
        }

        public void DrawGroundCheckGizmo()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }
}