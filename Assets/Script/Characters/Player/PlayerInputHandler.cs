using UnityEngine.InputSystem;
using Core.Logging;
using UnityEngine;
using System;

namespace Game.Characters.Player
{
    /// <summary>
    /// Handles all player input using New Input System (Plain C# - No MonoBehaviour)
    /// </summary>
    public class PlayerInputHandler
    {
        private readonly Core.Logging.ILogger _logger;
        private readonly InputSystem_Actions _actions;
        private Vector2 _moveInput;
        private Vector2 _aimDirection;
        private Camera _mainCamera;

        public Vector2 MoveInput => _moveInput;
        public Vector2 AimDirection => _aimDirection;
        public bool IsAimHeld { get; private set; }

        // Events
        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnAttackPressed;
        public event Action OnRightClickPressed;
        public event Action OnRightClickReleased;
        public event Action OnDashPressed;
        public event Action OnGrabPressed;
        public event Action OnSkill1Pressed;
        public event Action OnSkill2Pressed;

        // Constructor - DI via VContainer
        public PlayerInputHandler(LoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<PlayerInputHandler>();
            _actions = new InputSystem_Actions();

            _logger?.Log("PlayerInputHandler constructed");
        }

        public void Enable()
        {
            _actions.Player.Enable();

            // Movement
            _actions.Player.Move.performed += OnMove;
            _actions.Player.Move.canceled += OnMove;

            // Jump
            _actions.Player.Jump.performed += OnJump;
            _actions.Player.Jump.canceled += OnJumpCancel;

            // Attack
            _actions.Player.Attack.performed += OnAttack;

            // Right click is used as hold-to-aim mode for dash.
            _actions.Player.Aim.started += OnRightClickStarted;
            _actions.Player.Aim.canceled += OnRightClickCanceled;

            //Dash
            _actions.Player.Dash.performed += OnDash;

            // Grab — use .started to bypass the Hold interaction delay on the Interact action
            _actions.Player.Interact.started += OnInteractStarted;

            _actions.Player.Skill1.performed += OnSkill1;
            _actions.Player.Skill2.performed += OnSkill2;

            _logger?.Log("Input system enabled");
        }

        public void Disable()
        {
            _actions.Player.Move.performed -= OnMove;
            _actions.Player.Move.canceled -= OnMove;
            _actions.Player.Jump.performed -= OnJump;
            _actions.Player.Jump.canceled -= OnJumpCancel;
            _actions.Player.Attack.performed -= OnAttack;
            _actions.Player.Aim.started -= OnRightClickStarted;
            _actions.Player.Aim.canceled -= OnRightClickCanceled;
            _actions.Player.Dash.performed -= OnDash;
            _actions.Player.Interact.started -= OnInteractStarted;
            _actions.Player.Skill1.performed -= OnSkill1;
            _actions.Player.Skill2.performed -= OnSkill2;

            IsAimHeld = false;
            _actions.Player.Disable();

            _logger?.Log("Input system disabled");
        }

        public void Dispose()
        {
            Disable();
            _actions?.Dispose();
        }

        public void SetCamera(Camera camera)
        {
            _mainCamera = camera;
        }

        public void UpdateAimDirection(Transform playerTransform)
        {
            if (Mouse.current == null || _mainCamera == null || playerTransform == null)
                return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                new Vector3(mousePos.x, mousePos.y, _mainCamera.nearClipPlane)
            );
            Vector2 direction = (Vector2)worldPos - (Vector2)playerTransform.position;

            if (direction.magnitude > 0.1f)
            {
                _aimDirection = direction.normalized;
            }
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<UnityEngine.Vector2>();
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            OnJumpPressed?.Invoke();
            _logger?.Log("Jump pressed");
        }

        private void OnJumpCancel(InputAction.CallbackContext context)
        {
            OnJumpReleased?.Invoke();
            _logger?.Log("Jump released");
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            OnAttackPressed?.Invoke();
            _logger?.Log("Attack pressed");
        }

        private void OnRightClickStarted(InputAction.CallbackContext context)
        {
            IsAimHeld = true;
            OnRightClickPressed?.Invoke();
            _logger?.Log("Right click started - Dash aim mode on");
        }

        private void OnRightClickCanceled(InputAction.CallbackContext context)
        {
            IsAimHeld = false;
            OnRightClickReleased?.Invoke();
            _logger?.Log("Right click released - Dash aim mode off");
        }

        private void OnDash(InputAction.CallbackContext context)
        {
            OnDashPressed?.Invoke();
            _logger?.Log("Dash pressed");
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            OnGrabPressed?.Invoke();
            _logger?.Log("Interact pressed (grab)");
        }

        private void OnSkill1(InputAction.CallbackContext context)
        {
            OnSkill1Pressed?.Invoke();
            _logger?.Log("Skill1 pressed (EMP)");
        }

        private void OnSkill2(InputAction.CallbackContext context)
        {
            OnSkill2Pressed?.Invoke();
            _logger?.Log("Skill2 pressed (TrueDamage)");
        }
    }
}