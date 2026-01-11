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

        public Vector2 MoveInput => _moveInput;

        // Events
        public event Action OnJumpPressed;
        public event Action OnAttackPressed;

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

            // Attack
            _actions.Player.Attack.performed += OnAttack;

            _logger?.Log("Input system enabled");
        }

        public void Disable()
        {
            _actions.Player.Move.performed -= OnMove;
            _actions.Player.Move.canceled -= OnMove;
            _actions.Player.Jump.performed -= OnJump;
            _actions.Player.Attack.performed -= OnAttack;

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

        private void OnAttack(InputAction.CallbackContext context)
        {
            OnAttackPressed?.Invoke();
            _logger?.Log("Attack pressed");
        }
    }
}