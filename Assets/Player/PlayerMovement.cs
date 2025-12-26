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

    private bool isFacingRight = true;

    [Header("Dash / Slingshot")]
    public float pushForce = 15f; 
    public float dashControlLossTime = 0.3f; 
    private float dashTimer; 

    [Header("Ground Check")]
    public Transform groundCheckTransform;
    public float groundCheckRadius;
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
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}