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
        private readonly int _animIsWallSliding;
        private readonly int _animIsFalling;
        private readonly int _animAttack;
        private readonly int _animDeath;
        private readonly int _animRunSpeed;
        private readonly bool _hasWallSlideParameter;
        private readonly bool _hasFallingParameter;
        private readonly bool _hasRunSpeedParameter;

        // Run animation playback speed at min/max momentum
        private const float RunSpeedMultMin = 1.0f;
        private const float RunSpeedMultMax = 1.6f;

        // Constructor - DI via VContainer
        public PlayerAnimationController(Animator animator, LoggerFactory loggerFactory)
        {
            _animator = animator;
            _logger = loggerFactory?.CreateLogger<PlayerAnimationController>();

            // Cache animation parameters
            _animMoveSpeed = Animator.StringToHash("MoveSpeed");
            _animIsGrounded = Animator.StringToHash("IsGrounded");
            _animIsWallSliding = Animator.StringToHash("IsWallSliding");
            _animIsFalling = Animator.StringToHash("IsFalling");
            _animAttack = Animator.StringToHash("Attack");
            _animDeath = Animator.StringToHash("Death");
            _animRunSpeed = Animator.StringToHash("RunSpeed");
            _hasWallSlideParameter = HasBoolParameter(_animator, "IsWallSliding");
            _hasFallingParameter = HasBoolParameter(_animator, "IsFalling");
            _hasRunSpeedParameter = HasFloatParameter(_animator, "RunSpeed");

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

            if (_hasWallSlideParameter)
            {
                _animator.SetBool(_animIsWallSliding, _movementComponent.IsWallSliding);
            }

            // Airborne anim is driven by a continuous bool (not a one-shot trigger), so the
            // Jump → Fall flow always matches physics regardless of frame ordering.
            if (_hasFallingParameter)
            {
                _animator.SetBool(_animIsFalling, _movementComponent.IsFallingAnim);
            }

            if (_hasRunSpeedParameter)
            {
                float speedRatio = Mathf.Clamp01(moveSpeed / Mathf.Max(1f, _movementComponent.MaxSpeed));
                _animator.SetFloat(_animRunSpeed, Mathf.Lerp(RunSpeedMultMin, RunSpeedMultMax, speedRatio));
            }
        }

        private static bool HasBoolParameter(Animator animator, string parameterName)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == parameterName) return true;
            return false;
        }

        private static bool HasFloatParameter(Animator animator, string parameterName)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Float && p.name == parameterName) return true;
            return false;
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