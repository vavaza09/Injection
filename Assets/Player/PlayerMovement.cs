using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

//we will sperate all the script later for sure 
//script for player controller, jumping and dashing/slingshotting , UI
public class PlayerMovement : MonoBehaviour
{
    public InputSystem_Actions actions;
    Camera mainCam; 

    [Header("Movement Stats")]
    public float speed;
    public float jumpForce;
    float move;
    Rigidbody2D rb;

    private bool isFacingRight = true;

    [Header("Dash / Slingshot")]
    public float minPushForce = 15f;
    public float maxPushForce = 25f;
    public float chargeTime = 1.0f;
    public float minChargeThredhold = 0.7f;
    public float dashControlLossTime = 0.3f;
    
    private float dashTimer;
    private float currentCharge;
    private bool isCharging;

    [Header("UI & Feedback")]
    public Slider chargeBar;
    public Color chargingColor = Color.white;
    public Color readyColor = Color.green;

    [Header("Ground Check")]
    public Transform groundCheckTransform;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;
    bool isGrounded;

    //animation
    Animator animator;

    void Awake()
    {
        actions = new InputSystem_Actions();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
        animator = GetComponent<Animator>();

        if (chargeBar)
        {
            chargeBar.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        actions.Player.Enable();
        actions.Player.Move.performed += Movement;
        actions.Player.Move.canceled += Movement; 
        actions.Player.Jump.performed += Jumping;
        actions.Player.Jump.canceled += Jumping;

        actions.Player.Dash.started += ctx => { StartCharging(); };
        actions.Player.Dash.canceled += OnDashRelease;
    }

    void OnDisable()
    {
        actions.Player.Disable();
        actions.Player.Move.performed -= Movement;
        actions.Player.Move.canceled -= Movement;
        actions.Player.Jump.performed -= Jumping;
        actions.Player.Jump.canceled -= Jumping;

        actions.Player.Dash.canceled -= OnDashRelease;
    }

    void Movement(InputAction.CallbackContext ctx)
    {
        move = ctx.ReadValue<Vector2>().x;
    }

    void Jumping(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && isGrounded)
        {
            rb.linearVelocityY = jumpForce;
        }
    }

    private void StartCharging()
    {
        isCharging = true;
        currentCharge = 0f;

        if (chargeBar)
        {
            chargeBar.value = 0;
            chargeBar.gameObject.SetActive(true);
        }
    }

    void OnDashRelease(InputAction.CallbackContext ctx)
    {
        isCharging = false;
        if (chargeBar)
        {
            chargeBar.gameObject.SetActive(false);
        }

        if (currentCharge < minChargeThredhold)
        {
            currentCharge = 0f;
            return;
        }
        float chargePercent = Mathf.Clamp01(currentCharge / chargeTime);
        float finalForce = Mathf.Lerp(minPushForce, maxPushForce, chargePercent);

        Vector2 mouseWorldPosition = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        // Calculate direction: (Target - Current)
        Vector2 direction = (mouseWorldPosition - (Vector2)transform.position).normalized;

        

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * finalForce, ForceMode2D.Impulse);

        dashTimer = dashControlLossTime;

        animator.Play("Attack");

        if(direction.x >0 && !isFacingRight)
        {
            flip();

        }
        else if(direction.x < 0 && isFacingRight)
        {
            flip();
        }
    }

    private void flip()
    {
        isFacingRight = !isFacingRight;
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);

        // Counter-flip the UI 
        if (chargeBar != null)
        {
            Vector3 uiScale = chargeBar.transform.localScale;
            uiScale.x *= -1;
            chargeBar.transform.localScale = uiScale;
        }
    }

    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));

        if (isCharging)
        {
            currentCharge += Time.deltaTime;
            UpdateUI();
        }
        if (dashTimer > 0)
        {
            dashTimer -= Time.deltaTime;
            return;
        }

        rb.linearVelocityX = move * speed;

        if(move > 0 && !isFacingRight)
        {
            flip();
        }
        else if (move < 0 && isFacingRight)
        {
            flip();
        }
    }

    void UpdateUI()
    {
        if(!chargeBar) return;

        chargeBar.value = currentCharge / chargeTime;
        Image fillImage = chargeBar.fillRect.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.color = (currentCharge >= minChargeThredhold) ? readyColor : chargingColor;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}