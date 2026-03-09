using UnityEngine;
using System.Collections;

namespace Game.Components.Movement
{
    [System.Serializable]
    public class DashSettings
    {
        [Header("Dash Settings (Celeste-inspired)")]
        public float dashSpeed = 240f;
        public float endDashSpeed = 160f;
        public float endDashUpMult = 0.75f;
        public float dashTime = 0.15f;
        public float dashCooldown = 0.2f;
        public float dashRefillCooldown = 0.1f;
        public float dashAttackTime = 0.3f;

        [Header("Dash Correction")]
        public int dashCornerCorrection = 4;
        public int dashVFloorSnapDist = 3;

        [Header("Dash Count")]
        public int maxDashes = 1;
    }

    public class DashHandler : IDashHandler
    {
        private readonly DashSettings _settings;
        private Rigidbody2D _rb;

        private int _currentDashes;
        private float _dashCooldownTimer;
        private float _dashRefillCooldownTimer;
        private float _dashAttackTimer;
        private Vector2 _dashDir;
        private bool _isDashing;
        private bool _dashStartedOnGround;

        public bool IsDashing => _isDashing;
        public bool DashAttacking => _dashAttackTimer > 0;
        public Vector2 DashDir => _dashDir;
        public int CurrentDashes => _currentDashes;
        public int MaxDashes => _settings.maxDashes;
        public bool CanDash => _dashCooldownTimer <= 0 && _currentDashes > 0;

        public DashHandler(DashSettings settings)
        {
            _settings = settings;
            _currentDashes = settings.maxDashes;
        }

        public void Initialize(Rigidbody2D rb)
        {
            _rb = rb;
            _currentDashes = _settings.maxDashes;
            Debug.Log($"[DashHandler] Initialized with {_settings.maxDashes} dashes");
        }

        public bool StartDash(Vector2 direction, bool isGrounded)
        {
            if (!CanDash || direction == Vector2.zero)
            {
                Debug.LogWarning($"[DashHandler] Cannot dash! CanDash: {CanDash}, Direction: {direction}");
                return false;
            }

            _isDashing = true;
            _dashStartedOnGround = isGrounded;
            _currentDashes = Mathf.Max(0, _currentDashes - 1);

            _dashCooldownTimer = _settings.dashCooldown;
            _dashRefillCooldownTimer = _settings.dashRefillCooldown;
            _dashAttackTimer = _settings.dashAttackTime;
            _dashDir = direction.normalized;

            Debug.Log($"[DashHandler] Dash started! Direction: {_dashDir}, Dashes left: {_currentDashes}");
            return true;
        }

        public IEnumerator DashCoroutine(Vector2 direction, bool isGrounded)
        {
            if (!StartDash(direction, isGrounded))
                yield break;

            _rb.linearVelocity = Vector2.zero;

            yield return null;

            Vector2 dashVelocity = _dashDir * _settings.dashSpeed;
            _rb.linearVelocity = dashVelocity;

            Debug.Log($"[DashHandler] Dash velocity: {dashVelocity}");

            yield return new WaitForSeconds(_settings.dashTime);

            _isDashing = false;

            Vector2 endVelocity = _dashDir * _settings.endDashSpeed;

            if (endVelocity.y < 0)
            {
                endVelocity.y *= _settings.endDashUpMult;
            }

            _rb.linearVelocity = endVelocity;

            Debug.Log($"[DashHandler] Dash ended! End velocity: {endVelocity}");
        }

        public void UpdateTimers()
        {
            if (_dashCooldownTimer > 0)
                _dashCooldownTimer -= Time.deltaTime;

            if (_dashRefillCooldownTimer > 0)
                _dashRefillCooldownTimer -= Time.deltaTime;

            if (_dashAttackTimer > 0)
                _dashAttackTimer -= Time.deltaTime;
        }

        public bool RefillDash()
        {
            if (_dashRefillCooldownTimer > 0)
                return false;

            if (_currentDashes < _settings.maxDashes)
            {
                _currentDashes = _settings.maxDashes;
                Debug.Log($"[DashHandler] Dash refilled! Current: {_currentDashes}");
                return true;
            }

            return false;
        }

        public void ResetDash()
        {
            _currentDashes = _settings.maxDashes;
            _dashCooldownTimer = 0;
            _dashRefillCooldownTimer = 0;
            _dashAttackTimer = 0;
            _isDashing = false;
        }
    }
}
