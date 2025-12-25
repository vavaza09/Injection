using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerMovement : MonoBehaviour
{
    public InputSystem_Actions actions;

    [Header("Movement Stats")]
    public float speed;
    public float jumpForce;
    float move;
    Rigidbody2D rb;

    [Header("Ground Check")]
    public Transform groundCheckTransform;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    bool isGrounded;

    void Awake()
    {
        actions = new InputSystem_Actions();
    }
    void OnEnable()
    {
        actions.Player.Enable();
        actions.Player.Move.performed += Movement;
        actions.Player.Jump.performed += Jumping;

        actions.Player.Move.canceled += Movement;
        actions.Player.Jump.canceled += Jumping;
    }
    void OnDisable()
    {
        actions.Player.Disable();
        actions.Player.Move.performed -= Movement;
        actions.Player.Jump.performed -= Jumping;
    }

    void Movement(InputAction.CallbackContext ctx)
    {
        move = ctx.ReadValue<Vector2>().x;
    }

    void Jumping(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            if (isGrounded) { rb.linearVelocityY = jumpForce; }
        }
        
    }
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();   
    }


    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        rb.linearVelocityX = move * speed;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}

