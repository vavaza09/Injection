using ILogger = Core.Logging.ILogger;
using UnityEngine;
using Core.Logging;
using VContainer;

namespace Game.Components.Movement
{
    public class MovementComponent : MonoBehaviour
    {
        private ILogger _logger;

        [Header("Dependencies")]
        [SerializeField]private DashComponent _dashComponent;
        // Movement Settings
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float deceleration = 50f;

        // Air Control
        [Header("Air Control")]
        [SerializeField] private float airAcceleration = 30f;
        [SerializeField] private float airDeceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.8f;

        // Jump Settings (Celeste-inspired)
        [Header("Jump Settings (Celeste-inspired)")]
        [SerializeField] private float jumpSpeed = -105f;              // Celeste: JumpSpeed = -105f
        [SerializeField] private float jumpHBoost = 40f;               // Celeste: ความเร็วแนวนอนเพิ่มตอนกระโดด
        [SerializeField] private float varJumpTime = 0.2f;             // Celeste: VarJumpTime = .2f
        [SerializeField] private float jumpGraceTime = 0.1f;           // Celeste: JumpGraceTime = 0.1f (Coyote Time)
        
        [Header("Gravity Settings")]
        [SerializeField] private float gravity = 900f;                 // Celeste: Gravity = 900f
        [SerializeField] private float maxFall = 160f;                 // Celeste: MaxFall = 160f
        [SerializeField] private float fastMaxFall = 240f;             // Celeste: FastMaxFall = 240f
        [SerializeField] private float halfGravThreshold = 40f;        // Celeste: HalfGravThreshold = 40f
        
        [Header("Advanced Jump")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;   // เพิ่มแรงโน้มถ่วงตอนตก
        [SerializeField] private float lowJumpGravityMultiplier = 3f;  // แรงโน้มถ่วงตอนปล่อยปุ่มกระโดด
        [SerializeField] private float fallAirControlMultiplier = 0.75f;
        
        // Jump State
        private bool isJumping;
        private float jumpTimer;
        private bool isFalling;
        private float varJumpSpeed;                                    // Celeste: varJumpSpeed
        private float varJumpTimer;                                    // Celeste: varJumpTimer
        private float jumpGraceTimer;                                  // Celeste: jumpGraceTimer (Coyote Time)
        private bool autoJump;                                         // Celeste: AutoJump
        private float autoJumpTimer;                                   // Celeste: AutoJumpTimer
        private const float bounceAutoJumpTime = 0.1f;                 // Celeste: BounceAutoJumpTime
        
        // Ground Check
        [Header("Ground Check")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody2D rb;
        private Transform characterTransform;
        private Transform groundCheck;
        private bool isGrounded;
        private bool wasOnGround;                                      // Celeste: wasOnGround
        [SerializeField] private bool canMove = true;

        // Multi-jump
        [Header("Multi-Jump")]
        [SerializeField] private int maxJumps = 2;
        private int jumpsRemaining;

        // ⭐ ใช้ VContainer Inject แทน Constructor
        [Inject]
        public void Construct(LoggerFactory loggerFactory, DashComponent dashComponent)
        {
            _logger = loggerFactory?.CreateLogger<MovementComponent>();
            //_dashComponent = dashComponent;
            _logger?.Log("MovementComponent injected via VContainer");
        }

        // ⭐ ใช้ Awake แทน Constructor
        private void Awake()
        {
            if (groundLayer == 0)
            {
                groundLayer = LayerMask.GetMask("Ground");
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
            jumpsRemaining = maxJumps;

            // ⬅️ เพิ่ม: บังคับให้ gravityScale = 0
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
            if (_dashComponent != null)
            {
                _dashComponent.UpdateTimers();

                if (isGrounded)
                {
                    _dashComponent.RefillDash();
                }
            }

            if (isGrounded)
            {
                jumpsRemaining = maxJumps;
            }

            if (_dashComponent != null && _dashComponent.IsDashing) return;
            UpdateJumpState();
        }

        public void Move(Vector2 direction)
        {
            if (_dashComponent != null && _dashComponent.IsDashing) return;
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

        public void Dash(Vector2 direction)
        {
            _dashComponent?.StartDash(direction, isGrounded);
        }

        /// <summary>
        /// Updates the character's facing direction based on horizontal movement.
        /// Flips the localScale.x to face the direction of movement.
        /// </summary>
        public void UpdateFacing(Vector2 direction)
        {
            if (characterTransform == null) return;
            if (direction.x == 0) return;

            characterTransform.localScale = new Vector3(
                Mathf.Sign(direction.x),
                1,
                1
            );
        }

        /// <summary>
        /// Attempts a multi-jump. Returns true if the jump was performed.
        /// </summary>
        public bool TryJump(bool particles = true, bool playSfx = true)
        {
            if (jumpsRemaining <= 0) return false;

            Jump(particles, playSfx);
            jumpsRemaining--;
            return true;
        }

        /// <summary>
        /// Jump แบบ Celeste - มี Variable Jump Height + Jump Grace Time
        /// </summary>
        public void Jump(bool particles = true, bool playSfx = true)
        {
            Debug.Log($"🎯 Jump() START - Current velocity: {rb?.linearVelocity}");
            
            if (jumpGraceTimer <= 0 && !isGrounded)
            {
                Debug.LogWarning($"❌ Jump BLOCKED! jumpGraceTimer: {jumpGraceTimer:F3}, isGrounded: {isGrounded}");
                return;
            }

            if (!canMove || rb == null)
            {
                Debug.LogWarning($"❌ Jump BLOCKED! canMove: {canMove}, rb: {rb != null}");
                return;
            }

            Debug.Log($"✅ Jump SUCCESS! Applying velocity...");
            
            // Reset timers
            jumpGraceTimer = 0;
            varJumpTimer = varJumpTime;
            autoJump = true;
            
            // ⭐ แก้ไข: รักษาความเร็วแนวนอนเดิม + เพิ่ม jumpHBoost แบบจำกัด
            float currentSpeedX = rb.linearVelocity.x;
            float moveDirection = Mathf.Sign(currentSpeedX);
            
            // ถ้ากำลังวิ่งอยู่ในทิศทางเดียวกัน -> เพิ่ม jumpHBoost เล็กน้อย
            // ถ้าหยุดนิ่ง -> ไม่เพิ่ม
            float jumpBoostAmount = 0;
            if (Mathf.Abs(currentSpeedX) > 0.5f) // ถ้าวิ่งอยู่
            {
                // จำกัด jumpHBoost ไม่ให้เกิน moveSpeed
                float maxBoost = Mathf.Min(jumpHBoost, moveSpeed - Mathf.Abs(currentSpeedX));
                jumpBoostAmount = maxBoost * moveDirection;
            }
            
            // ตั้งค่า velocity แบบรักษาความเร็ว X เดิม
            Vector2 newVelocity = new Vector2(
                currentSpeedX + jumpBoostAmount,  // รักษาความเร็ว X + boost เล็กน้อย
                jumpSpeed  // ตั้ง Y เป็นความเร็วกระโดด
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

        /// <summary>
        /// อัพเดทสถานะการกระโดด - Celeste Style
        /// </summary>
        private void UpdateJumpState()
        {
            if (rb == null) return;

            // บันทึกสถานะพื้นก่อนหน้า
            wasOnGround = isGrounded;

            // รีเซ็ตสถานะเมื่อลงพื้น
            if (isGrounded)
            {
                isJumping = false;
                isFalling = false;
                jumpTimer = 0f;
                jumpGraceTimer = jumpGraceTime;
                autoJump = false;
                return;
            }

            // ลด Jump Grace Timer (Coyote Time)
            if (jumpGraceTimer > 0)
            {
                jumpGraceTimer -= Time.deltaTime;
            }

            // ลด Auto Jump Timer
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

            // Variable Jump - ถ้ายังกดปุ่มอยู่ให้กระโดดสูงขึ้น
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

                // Celeste: Half gravity when rising slowly
                if (Mathf.Abs(rb.linearVelocity.y) < halfGravThreshold && (autoJump || varJumpTimer > 0))
                {
                    gravityMultiplier = 0.5f;
                    Debug.Log($"🔵 Half Gravity: {gravityMultiplier}");
                }
                // Fast fall multiplier
                else if (rb.linearVelocity.y > 0)  // ⬅️ เปลี่ยนจาก < 0 เป็น > 0 (Unity Y+ = ลง)
                {
                    gravityMultiplier = fallGravityMultiplier;
                    Debug.Log($"🔴 Fast Fall: {gravityMultiplier}");
                }
                // Low jump multiplier (ถ้าปล่อยปุ่มกระโดด)
                else if (rb.linearVelocity.y < 0 && varJumpTimer <= 0)  // ⬅️ เปลี่ยนจาก > 0 เป็น < 0
                {
                    gravityMultiplier = lowJumpGravityMultiplier;
                    Debug.Log($"🟡 Low Jump: {gravityMultiplier}");
                }

                // ⭐ แก้สูตร Gravity (เพิ่มเลขลบ)
                float gravityForce = gravity * gravityMultiplier * Time.deltaTime;
                
                // ⬅️ เปลี่ยนจาก + เป็น - (เพราะ gravity เป็นเลขบวก แต่ต้องดึงลง)
                rb.linearVelocity = new Vector2(
                    rb.linearVelocity.x,
                    rb.linearVelocity.y - gravityForce  // ⬅️ เปลี่ยนเป็น ลบ (-)
                );

                Debug.Log($"⬇️ Applying Gravity: {gravityForce:F2}, Current Y: {rb.linearVelocity.y:F2}");

                // Cap fall speed (Unity Y+ = ลง, ดังนั้นใช้ >)
                float maxFallSpeed = maxFall;
                if (rb.linearVelocity.y > maxFallSpeed)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
                    Debug.Log($"🛑 Capped fall speed to: {maxFallSpeed}");
                }
            }

            // Update falling state (Unity Y+ = ลง)
            if (rb.linearVelocity.y > 0)
            {
                isFalling = true;
                isJumping = false;
            }
        }

        /// <summary>
        /// ยกเลิกการกระโดด (เมื่อปล่อยปุ่ม) - Celeste Style
        /// </summary>
        public void CancelJump()
        {
            if (varJumpTimer > 0)
            {
                varJumpTimer = 0;
                _logger?.Log("Jump cancelled (released button)");
            }
        }

        /// <summary>
        /// Bounce - Celeste Style (ใช้กับ Spring, Bouncer)
        /// </summary>
        public void Bounce(float fromY, float bounceSpeed = -140f)
        {
            if (rb == null) return;

            // Move to bounce position
            float bottomY = characterTransform.position.y - (groundCheckSize.y / 2);
            MoveVExact((int)(fromY - bottomY));

            // Reset state
            isJumping = false;
            isFalling = false;
            jumpGraceTimer = 0;
            varJumpTimer = 0.2f; // Celeste: BounceVarJumpTime
            autoJump = true;
            autoJumpTimer = bounceAutoJumpTime;

            // Set velocity
            varJumpSpeed = bounceSpeed;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceSpeed);

            _logger?.Log($"Bounced! Speed: {bounceSpeed}");
        }

        /// <summary>
        /// Start Jump Grace Time - เรียกเมื่อเดินออกจากขอบแพลตฟอร์ม
        /// </summary>
        public void StartJumpGraceTime()
        {
            jumpGraceTimer = jumpGraceTime;
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

        /// <summary>
        /// Getters สำหรับ Auto Jump (ใช้กับ Spring, Bouncer)
        /// </summary>
        public bool AutoJump
        {
            get => autoJump;
            set => autoJump = value;
        }


        private void CheckGroundStatus()
        {
            bool previousGrounded = isGrounded;
            
            if (groundCheck != null)
            {
                isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
            }

            // ⬅️ เพิ่ม Debug
            if (previousGrounded != isGrounded)
            {
                Debug.Log($"Ground State Changed: {previousGrounded} → {isGrounded}");
            }

            if (previousGrounded && !isGrounded && rb != null && rb.linearVelocity.y >= 0)
            {
                StartJumpGraceTime();
                Debug.Log("🕐 Started Jump Grace Time (Coyote Time)");  // ⬅️ เพิ่ม
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        public int GetJumpsRemaining()
        {
            return jumpsRemaining;
        }

        public void SetMaxJumps(int max)
        {
            maxJumps = max;
            jumpsRemaining = max;
        }
    }
}