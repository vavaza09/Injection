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
        private readonly int _animIsHanging;
        private readonly int _animIsDashing;
        private readonly int _animDashDirection;
        private readonly int _animIsDashAttacking;
        private readonly int _animIsKnockback;
        private readonly bool _hasWallSlideParameter;
        private readonly bool _hasFallingParameter;
        private readonly bool _hasRunSpeedParameter;
        private readonly bool _hasHangingParameter;
        private readonly bool _hasDashingParameter;
        private readonly bool _hasDashAttackingParameter;
        private readonly bool _hasKnockbackParameter;

        // Run animation playback speed at min/max momentum
        private const float RunSpeedMultMin = 1.0f;
        private const float RunSpeedMultMax = 1.6f;

        // Dash direction thresholds (settable from Player via SetDashThresholds)
        private float _dashUpThreshold = 0.5f;
        private float _dashDownThreshold = -0.5f;

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
            _animIsHanging = Animator.StringToHash("IsHanging");
            _animIsDashing = Animator.StringToHash("IsDashing");
            _animDashDirection = Animator.StringToHash("DashDirection");
            _animIsDashAttacking = Animator.StringToHash("IsDashAttacking");
            _animIsKnockback = Animator.StringToHash("IsKnockback");
            _hasWallSlideParameter = HasBoolParameter(_animator, "IsWallSliding");
            _hasFallingParameter = HasBoolParameter(_animator, "IsFalling");
            _hasRunSpeedParameter = HasFloatParameter(_animator, "RunSpeed");
            _hasHangingParameter = HasBoolParameter(_animator, "IsHanging");
            _hasDashingParameter = HasBoolParameter(_animator, "IsDashing");
            _hasDashAttackingParameter = HasBoolParameter(_animator, "IsDashAttacking");
            _hasKnockbackParameter = HasTriggerParameter(_animator, "IsKnockback");

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

            if (_hasHangingParameter)
            {
                _animator.SetBool(_animIsHanging, _movementComponent.IsGrabbing);
            }

            if (_hasDashingParameter)
            {
                bool isDashing = _movementComponent.IsDashing;
                _animator.SetBool(_animIsDashing, isDashing);
                if (isDashing)
                {
                    Vector2 dir = _movementComponent.DashDirection;
                    int dashDir = dir.y >= _dashUpThreshold ? 1 : dir.y <= _dashDownThreshold ? 2 : 0;
                    _animator.SetInteger(_animDashDirection, dashDir);
                }
            }

            if (_hasDashAttackingParameter)
            {
                _animator.SetBool(_animIsDashAttacking, _movementComponent.IsDashAttacking);
            }
        }

        public void SetDashThresholds(float upThreshold, float downThreshold)
        {
            _dashUpThreshold = upThreshold;
            _dashDownThreshold = downThreshold;
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

        private static bool HasTriggerParameter(Animator animator, string parameterName)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == parameterName) return true;
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

        public void PlayKnockbackAnimation()
        {
            if (_animator != null && _hasKnockbackParameter)
            {
                _animator.SetTrigger(_animIsKnockback);
                _logger?.Log("Knockback animation played");
            }
        }
    }
}