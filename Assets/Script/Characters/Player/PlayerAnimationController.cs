using UnityEngine;
using Game.Components.Movement;
using Core.Logging;

namespace Game.Characters.Player
{
    /// <summary>
    /// Controls player animation (Plain C# - No MonoBehaviour)
    /// </summary>
    public class PlayerAnimationController
    {
        private readonly Core.Logging.ILogger _logger;
        private readonly Animator _animator;
        private MovementComponent _movementComponent;

        // Animation parameter hashes
        private readonly int _animMoveSpeed;
        private readonly int _animIsGrounded;
        private readonly int _animIsClimbing;
        private readonly int _animAttack;
        private readonly int _animJump;
        private readonly int _animDeath;
        private readonly bool _hasClimbParameter;

        // Constructor - DI via VContainer
        public PlayerAnimationController(Animator animator, LoggerFactory loggerFactory)
        {
            _animator = animator;
            _logger = loggerFactory?.CreateLogger<PlayerAnimationController>();

            // Cache animation parameters
            _animMoveSpeed = Animator.StringToHash("MoveSpeed");
            _animIsGrounded = Animator.StringToHash("IsGrounded");
            _animIsClimbing = Animator.StringToHash("IsClimbing");
            _animAttack = Animator.StringToHash("Attack");
            _animJump = Animator.StringToHash("Jump");
            _animDeath = Animator.StringToHash("Death");
            _hasClimbParameter = HasBoolParameter(_animator, "IsClimbing");

            _logger?.Log("PlayerAnimationController initialized");
        }

        public void SetMovementComponent(MovementComponent movementComponent)
        {
            _movementComponent = movementComponent;
            _logger?.Log("MovementComponent assigned to AnimationController");
        }

        public void UpdateMovementAnimation()
        {
            if (_animator == null || _movementComponent == null) return;

            float moveSpeed = Mathf.Abs(_movementComponent.GetVelocity().x);
            _animator.SetFloat(_animMoveSpeed, moveSpeed);
            _animator.SetBool(_animIsGrounded, _movementComponent.IsGrounded());

            if (_hasClimbParameter)
            {
                _animator.SetBool(_animIsClimbing, _movementComponent.IsClimbing());
            }
        }

        private static bool HasBoolParameter(Animator animator, string parameterName)
        {
            if (animator == null) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        public void PlayJumpAnimation()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_animJump);
                _logger?.Log("Jump animation played");
            }
        }

        public void PlayAttackAnimation()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_animAttack);
                _logger?.Log("Attack animation played");
            }
        }

        public void PlayDeathAnimation()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_animDeath);
                _logger?.Log("Death animation played");
            }
        }
    }
}