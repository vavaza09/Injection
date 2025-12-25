using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public InputSystem_Actions actions;
    Camera mainCam; 

    [Header("Movement Stats")]
    public float speed;
    public float jumpForce;
    float move;
    Rigidbody2D rb;

    [Header("Dash / Slingshot")]
    public float pushForce = 15f; 
    public float dashControlLossTime = 0.3f; 
    private float dashTimer; 

    [Header("Ground Check")]
    public Transform groundCheckTransform;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    bool isGrounded;

    void Awake()
    {
        actions = new InputSystem_Actions();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main; 
    }

    void OnEnable()
    {
        actions.Player.Enable();
        actions.Player.Move.performed += Movement;
        actions.Player.Move.canceled += Movement; 
        actions.Player.Jump.performed += Jumping;
        actions.Player.Jump.canceled += Jumping;

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

    void OnDashRelease(InputAction.CallbackContext ctx)
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPosition = mainCam.ScreenToWorldPoint(mouseScreenPosition);

        // Calculate direction: (Target - Current)
        Vector2 direction = (mouseWorldPosition - (Vector2)transform.position).normalized;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * pushForce, ForceMode2D.Impulse);

        dashTimer = dashControlLossTime;
    }

    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);

        if (dashTimer > 0)
        {
            dashTimer -= Time.deltaTime;
            return;
        }

        rb.linearVelocityX = move * speed;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}