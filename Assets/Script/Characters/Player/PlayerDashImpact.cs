using UnityEngine;
using Game.Components.Combat;
using System.Collections.Generic;
using System;

public class PlayerDashImpact : MonoBehaviour
{
    [Header("Dash Impact Settings")]
    [SerializeField] private float dashImpactBaseDamage = 15f;
    [SerializeField] private float dashImpactCooldown = 0.15f;
    [SerializeField] private LayerMask dashDamageLayer = ~0;

    [Header("Hitstop")]
    [SerializeField] private float hitstopDuration = 1f;
    [SerializeField] private float hitstopTimeScale = 0.2f;

    [Header("Weak Point Hit Feedback")]
    [SerializeField] private float weakPointShakeIntensity = 0.1f;

    public event Action ImpactLanded;

    private AttackComponent _attackComponent;
    private MovementComponentRef _movement;
    private HitFlash _hitFlash;
    private readonly HashSet<int> _dashHitTargets = new HashSet<int>();
    private bool _wasDashing;

    // Thin wrapper so PlayerDashImpact can read IsDashing/DashAttacking without a hard assembly reference
    private class MovementComponentRef
    {
        private readonly Game.Components.Movement.MovementComponent _mc;
        public MovementComponentRef(Game.Components.Movement.MovementComponent mc) => _mc = mc;
        public bool IsDashing     => _mc != null && _mc.IsDashing;
        public bool DashAttacking => _mc != null && _mc.DashAttacking;
    }

    public void Initialize(Game.Components.Movement.MovementComponent movementComponent, float cooldownOverride = -1f)
    {
        _movement = new MovementComponentRef(movementComponent);
        float cooldown = cooldownOverride >= 0f ? cooldownOverride : dashImpactCooldown;
        _attackComponent = new AttackComponent(cooldown);
        _hitFlash = GetComponentInChildren<HitFlash>();
    }

    private void Update()
    {
        if (_movement == null) return;

        bool isDashingNow = _movement.IsDashing;
        if (isDashingNow && !_wasDashing)
            _dashHitTargets.Clear();
        _wasDashing = isDashingNow;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealDashImpactDamage(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDashImpactDamage(other);
    }

    private void TryDealDashImpactDamage(Collider2D targetCollider)
    {
        if (_movement == null || targetCollider == null) return;
        if (!_movement.IsDashing || !_movement.DashAttacking) return;
        if ((dashDamageLayer.value & (1 << targetCollider.gameObject.layer)) == 0) return;
        if (_attackComponent == null || !_attackComponent.CanAttack()) return;

        character targetCharacter = targetCollider.GetComponentInParent<character>();
        if (targetCharacter == null || targetCharacter == GetComponentInParent<character>()) return;

        bool hitWeakPoint = false;
        if (targetCharacter is Enemy)
        {
            EnemyWeakPoint weakPoint = targetCollider.GetComponent<EnemyWeakPoint>();
            if (weakPoint == null)
            {
                EnemyWeakPoint[] weakPoints = targetCharacter.GetComponentsInChildren<EnemyWeakPoint>();
                for (int i = 0; i < weakPoints.Length; i++)
                {
                    if (weakPoints[i] != null && weakPoints[i].IsWeakPoint(targetCollider))
                    {
                        weakPoint = weakPoints[i];
                        break;
                    }
                }
            }

            if (weakPoint == null) return;
            if (weakPoint.OwnerEnemy != null && weakPoint.OwnerEnemy != targetCharacter) return;
            hitWeakPoint = true;
        }

        int targetId = targetCharacter.GetInstanceID();
        if (_dashHitTargets.Contains(targetId)) return;

        _attackComponent.PerformAttack(targetCharacter, dashImpactBaseDamage);
        _dashHitTargets.Add(targetId);
        ImpactLanded?.Invoke();

        if (hitWeakPoint)
        {
            SlowMotion.Instance.StartSlowMotion(hitstopTimeScale, hitstopDuration);
            CameraManager.instance?.Shake(weakPointShakeIntensity);
            _hitFlash?.Flash();
            HitFlashFX.Spawn(targetCollider.bounds.center);
            SoundManager.PlaySound(SoundType.HITSTOP);
            targetCharacter.Die();
        }
    }
}
