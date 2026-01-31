using UnityEngine;
using System.Collections;

namespace Game.Components.Movement
{
    public class DashComponent : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Dash Settings (Celeste-inspired)")]
        [SerializeField] private float dashSpeed = 240f;           // Celeste: DashSpeed = 240f
        [SerializeField] private float endDashSpeed = 160f;        // Celeste: EndDashSpeed = 160f
        [SerializeField] private float endDashUpMult = 0.75f;      // Celeste: EndDashUpMult = .75f
        [SerializeField] private float dashTime = 0.15f;           // Celeste: DashTime = .15f
        [SerializeField] private float dashCooldown = 0.2f;        // Celeste: DashCooldown = .2f
        [SerializeField] private float dashRefillCooldown = 0.1f;  // Celeste: DashRefillCooldown = .1f
        [SerializeField] private float dashAttackTime = 0.3f;      // Celeste: DashAttackTime = .3f
        
        [Header("Dash Correction")]
        [SerializeField] private int dashCornerCorrection = 4;     // Celeste: DashCornerCorrection = 4
        [SerializeField] private int dashVFloorSnapDist = 3;       // Celeste: DashVFloorSnapDist = 3
        
        [Header("Dash Count")]
        [SerializeField] private int maxDashes = 1;                // จำนวน Dash สูงสุด
        
        [Header("References")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Transform characterTransform;
        
        #endregion
        
        #region Private Fields
        
        private int currentDashes;
        private float dashCooldownTimer;
        private float dashRefillCooldownTimer;
        private float dashAttackTimer;
        private Vector2 dashDir;
        private bool isDashing;
        private bool dashStartedOnGround;
        private Coroutine dashCoroutine;
        
        #endregion
        
        #region Properties
        
        public bool IsDashing => isDashing;
        public bool DashAttacking => dashAttackTimer > 0;
        public Vector2 DashDir => dashDir;
        public int CurrentDashes => currentDashes;
        public int MaxDashes => maxDashes;
        
        public bool CanDash => dashCooldownTimer <= 0 && currentDashes > 0;
        
        #endregion
        
        #region Initialization
        
        public void Initialize(Rigidbody2D rigidbody, Transform transform)
        {
            rb = rigidbody;
            characterTransform = transform;
            currentDashes = maxDashes;
            
            Debug.Log($"[DashComponent] Initialized with {maxDashes} dashes");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// เริ่ม Dash ตามทิศทางที่กำหนด
        /// </summary>
        public bool StartDash(Vector2 direction, bool isGrounded)
        {
            if (!CanDash || direction == Vector2.zero)
            {
                Debug.LogWarning($"[DashComponent] Cannot dash! CanDash: {CanDash}, Direction: {direction}");
                return false;
            }
            
            // Stop existing dash
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
            }
            
            // Start new dash
            dashCoroutine = StartCoroutine(DashCoroutine(direction, isGrounded));
            return true;
        }
        
        /// <summary>
        /// Update dash timers (เรียกใน Update)
        /// </summary>
        public void UpdateTimers()
        {
            // Dash Cooldown
            if (dashCooldownTimer > 0)
            {
                dashCooldownTimer -= Time.deltaTime;
            }
            
            // Dash Refill Cooldown
            if (dashRefillCooldownTimer > 0)
            {
                dashRefillCooldownTimer -= Time.deltaTime;
            }
            
            // Dash Attack Timer
            if (dashAttackTimer > 0)
            {
                dashAttackTimer -= Time.deltaTime;
            }
        }
        
        /// <summary>
        /// Refill Dash (เรียกเมื่อลงพื้น)
        /// </summary>
        public bool RefillDash()
        {
            if (dashRefillCooldownTimer > 0)
            {
                return false;
            }
            
            if (currentDashes < maxDashes)
            {
                currentDashes = maxDashes;
                Debug.Log($"[DashComponent] Dash refilled! Current: {currentDashes}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Reset Dash State
        /// </summary>
        public void ResetDash()
        {
            currentDashes = maxDashes;
            dashCooldownTimer = 0;
            dashRefillCooldownTimer = 0;
            dashAttackTimer = 0;
            isDashing = false;
        }
        
        #endregion
        
        #region Coroutines
        
        private IEnumerator DashCoroutine(Vector2 direction, bool isGrounded)
        {
            // ===== DASH BEGIN =====
            isDashing = true;
            dashStartedOnGround = isGrounded;
            currentDashes = Mathf.Max(0, currentDashes - 1);
            
            dashCooldownTimer = dashCooldown;
            dashRefillCooldownTimer = dashRefillCooldown;
            dashAttackTimer = dashAttackTime;
            
            dashDir = direction.normalized;
            
            Debug.Log($"[DashComponent] Dash started! Direction: {dashDir}, Dashes left: {currentDashes}");
            
            // Stop current velocity
            rb.linearVelocity = Vector2.zero;
            
            yield return null; // รอ 1 frame
            
            // Set dash velocity
            Vector2 dashVelocity = dashDir * dashSpeed;
            rb.linearVelocity = dashVelocity;
            
            Debug.Log($"[DashComponent] Dash velocity: {dashVelocity}");
            
            // Dash duration
            yield return new WaitForSeconds(dashTime);
            
            // ===== DASH END =====
            isDashing = false;
            
            // Calculate end velocity
            Vector2 endVelocity = dashDir * endDashSpeed;
            
            // Apply upward multiplier if dashing up
            if (endVelocity.y < 0)
            {
                endVelocity.y *= endDashUpMult;
            }
            
            rb.linearVelocity = endVelocity;
            
            Debug.Log($"[DashComponent] Dash ended! End velocity: {endVelocity}");
            
            dashCoroutine = null;
        }
        
        #endregion
    }
}
