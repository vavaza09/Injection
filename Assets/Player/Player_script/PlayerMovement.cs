using UnityEngine;

// This script controls the PHYSICAL body of the player.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public PlayerHUD playerHUD; 
    private Rigidbody2D rb;
    private Animator animator;

    [Header("Movement Stats")]
    public float speed = 5f;
    public float jumpForce = 10f;
    private float moveInput;
    private bool isFacingRight = true;

    [Header("Dash / Slingshot")]
    public float minPushForce = 15f;
    public float maxPushForce = 25f;
    public float chargeTime = 1.0f;
    public float minChargeThreshold = 0.7f;
    public float dashControlLossTime = 0.3f;

    // State Variables
    private float dashTimer;
    private float currentCharge;
    private bool isCharging;
    private bool isGrounded;

    [Header("Ground Check")]
    public Transform groundCheckTransform;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 1. Physics Checks
        isGrounded = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);

        // 2. Dash Timer Cooldown
        if (dashTimer > 0)
        {
            dashTimer -= Time.deltaTime;
            return;
        }

        // 3. Normal Movement Logic
        HandleMovement();
        HandleCharging();

        // 4. Animation
        animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
    }

    private void HandleMovement()
    {
        // Move the RB based on input received from Controller
        rb.linearVelocityX = moveInput * speed;

        // Flip logic
        if (moveInput > 0 && !isFacingRight) Flip();
        else if (moveInput < 0 && isFacingRight) Flip();
    }

    private void HandleCharging()
    {
        if (isCharging)
        {
            currentCharge += Time.deltaTime;

            // Update the UI via our reference
            if (playerHUD)
            {
                float chargePercent = Mathf.Clamp01(currentCharge / chargeTime);
                bool isReady = currentCharge >= minChargeThreshold;
                playerHUD.UpdateChargeBar(chargePercent, isReady);
            }
        }
    }

    // --- Public Methods called by InputController ---

    public void SetMoveInput(float input)
    {
        moveInput = input;
    }

    public void TryJump()
    {
        if (isGrounded && dashTimer <= 0)
        {
            rb.linearVelocityY = jumpForce;
        }
    }

    public void StartChargingDash()
    {
        isCharging = true;
        currentCharge = 0f;
        if (playerHUD) playerHUD.ToggleChargeBar(true);
    }

    public void ReleaseDash(Vector2 direction)
    {
        isCharging = false;
        if (playerHUD) playerHUD.ToggleChargeBar(false);

        // Check if we held it long enough
        if (currentCharge < minChargeThreshold)
        {
            currentCharge = 0f;
            return; // Failed dash (fizzle)
        }

        // Calculate Force
        float chargePercent = Mathf.Clamp01(currentCharge / chargeTime);
        float finalForce = Mathf.Lerp(minPushForce, maxPushForce, chargePercent);

        // Apply Physics
        rb.linearVelocity = Vector2.zero; // Stop current momentum for snappy feel
        rb.AddForce(direction * finalForce, ForceMode2D.Impulse);

        // Set Cooldown
        dashTimer = dashControlLossTime;

        // Visuals
        animator.Play("Attack");

        // Face the dash direction
        if (direction.x > 0 && !isFacingRight) Flip();
        else if (direction.x < 0 && isFacingRight) Flip();
    }

    // --- Helper Methods ---

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        // Flip the player
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;

        // Tell the UI to fix itself so it doesn't look backwards
        if (playerHUD) playerHUD.FlipUI();
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheckTransform)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
        }
    }
}