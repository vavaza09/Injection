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

        [Header("Dash Behavior")]
        public bool forceHorizontalDash = false;
        public bool stopAtDashEnd = true;
        [Range(0.1f, 1f)] public float airEndDashCarryMultiplier = 0.65f;
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
        private float _dashSpeedMultiplier = 1f;

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

        public bool StartDash(Vector2 direction, bool isGrounded, float speedMultiplier = 1f)
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
            Vector2 dashDirection = direction;
            if (_settings.forceHorizontalDash)
            {
                float x = Mathf.Abs(direction.x) > 0.01f ? direction.x : 1f;
                dashDirection = new Vector2(Mathf.Sign(x), 0f);
            }

            _dashDir = dashDirection.normalized;
            _dashSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);

            Debug.Log($"[DashHandler] Dash started! Direction: {_dashDir}, Dashes left: {_currentDashes}, Multiplier: {_dashSpeedMultiplier:F2}");
            return true;
        }

        public IEnumerator DashCoroutine(Vector2 direction, bool isGrounded, float speedMultiplier = 1f)
        {
            if (!StartDash(direction, isGrounded, speedMultiplier))
                yield break;

            _rb.linearVelocity = Vector2.zero;

            yield return null;

            Vector2 dashVelocity = _dashDir * (_settings.dashSpeed * _dashSpeedMultiplier);
            _rb.linearVelocity = dashVelocity;

            Debug.Log($"[DashHandler] Dash velocity: {dashVelocity}");

            yield return new WaitForSeconds(_settings.dashTime);

            _isDashing = false;

            Vector2 endVelocity = BuildEndVelocity();

            if (_settings.stopAtDashEnd)
            {
                if (_dashStartedOnGround)
                {
                    _rb.linearVelocity = Vector2.zero;
                    Debug.Log("[DashHandler] Ground dash ended and velocity stopped.");
                }
                else
                {
                    // Keep carry speed in air so horizontal control does not feel locked after dash.
                    _rb.linearVelocity = endVelocity;
                    Debug.Log($"[DashHandler] Air dash ended with carry velocity: {endVelocity}");
                }
                yield break;
            }

            _rb.linearVelocity = endVelocity;

            Debug.Log($"[DashHandler] Dash ended! End velocity: {endVelocity}");
        }

        private Vector2 BuildEndVelocity()
        {
            float dashSpeed = Mathf.Max(0f, _settings.dashSpeed * _dashSpeedMultiplier);
            float configuredEndSpeed = Mathf.Max(0f, _settings.endDashSpeed * _dashSpeedMultiplier);
            float carrySpeed = configuredEndSpeed > 0f ? Mathf.Min(configuredEndSpeed, dashSpeed) : dashSpeed;

            Vector2 endVelocity = _dashDir * carrySpeed;
            if (endVelocity.y < 0)
            {
                endVelocity.y *= _settings.endDashUpMult;
            }

            if (!_dashStartedOnGround)
            {
                endVelocity.x *= _settings.airEndDashCarryMultiplier;
            }

            return endVelocity;
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
