using UnityEngine.InputSystem;
using Core.Logging;
using UnityEngine;
using System;
using Game.Components.Movement;

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

        public Vector2 MoveInput => _moveInput;

        // Events
        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnAttackPressed;
        public event Action OnRightClickPressed; // ⬅️ เพิ่ม Event สำหรับคลิกขวา
        public event Action OnDashPressed;

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

            // Right Click (Slow Motion) - ⬅️ เพิ่มการ Subscribe
            _actions.Player.Aim.performed += OnRightClick;

            //Dash 
            _actions.Player.Dash.performed += OnDash;

            _logger?.Log("Input system enabled");
        }

        public void Disable()
        {
            _actions.Player.Move.performed -= OnMove;
            _actions.Player.Move.canceled -= OnMove;
            _actions.Player.Jump.performed -= OnJump;
            _actions.Player.Jump.canceled -= OnJumpCancel;
            _actions.Player.Attack.performed -= OnAttack;
            _actions.Player.Aim.performed -= OnRightClick;  // ⬅️ เพิ่ม Unsubscribe
            _actions.Player.Dash.performed -= OnDash;

            _actions.Player.Disable();

            _logger?.Log("Input system disabled");
        }

        public void Dispose()
        {
            Disable();
            _actions?.Dispose();
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

        // ⬅️ เพิ่มเมธอดสำหรับ Right Click
        private void OnRightClick(InputAction.CallbackContext context)
        {
            OnRightClickPressed?.Invoke();
            _logger?.Log("Right Click pressed - Slow Motion activated");
        }

        private void OnDash(InputAction.CallbackContext context)
        {
            OnDashPressed?.Invoke();
            _logger?.Log("Dash pressed");
        }
    }
}