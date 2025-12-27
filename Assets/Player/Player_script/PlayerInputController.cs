using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement movementScript; 
    private InputSystem_Actions actions;
    private Camera mainCam;

    void Awake()
    {
        actions = new InputSystem_Actions();
        mainCam = Camera.main;
    }

    void OnEnable()
    {
        actions.Player.Enable();

        // Movement
        actions.Player.Move.performed += ctx => movementScript.SetMoveInput(ctx.ReadValue<Vector2>().x);
        actions.Player.Move.canceled += ctx => movementScript.SetMoveInput(0f);

        // Jump
        actions.Player.Jump.performed += ctx => movementScript.TryJump();

        // Dash Logic
        actions.Player.Dash.started += ctx => movementScript.StartChargingDash();
        actions.Player.Dash.canceled += OnDashCanceled;
    }

    void OnDisable()
    {
        actions.Player.Disable();
        
        actions.Player.Move.performed -= ctx => movementScript.SetMoveInput(ctx.ReadValue<Vector2>().x);
        actions.Player.Move.canceled -= ctx => movementScript.SetMoveInput(0f);
        actions.Player.Jump.performed -= ctx => movementScript.TryJump();
        actions.Player.Dash.started -= ctx => movementScript.StartChargingDash();
        actions.Player.Dash.canceled -= OnDashCanceled;
    }

    private void OnDashCanceled(InputAction.CallbackContext ctx)
    {
        // Calculate the direction based on Mouse Position
        Vector2 mouseWorldPosition = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 direction = (mouseWorldPosition - (Vector2)transform.position).normalized;

        movementScript.ReleaseDash(direction);
    }
}