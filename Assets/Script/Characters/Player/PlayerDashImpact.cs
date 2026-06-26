using UnityEngine;
using Game.Components.Combat;
using System.Collections;
using System.Collections.Generic;
using System;

public class PlayerDashImpact : MonoBehaviour, Game.Components.Skills.ITrueDamageTarget
{
    [Header("Dash Impact Settings")]
    [SerializeField] private float dashImpactBaseDamage = 15f;
    [SerializeField] private float dashImpactCooldown = 0.15f;
    [SerializeField] private LayerMask dashDamageLayer = ~0;

    [Header("Hitstop")]
    [SerializeField] private float hitstopDuration = 1f;
    [SerializeField] private float hitstopTimeScale = 0.2f;
    [SerializeField] private float dashAttackFreezeDuration = 0.2f;

    [Header("Weak Point Hit Feedback")]
    [SerializeField] private float weakPointShakeIntensity = 0.1f;

    [Header("Bounce on Hit")]
    [SerializeField] private float bounceForce = 80f;
    [SerializeField] private float bounceUpwardBias = 0.2f;

    public event Action ImpactLanded;
    public event Action TrueDamageConsumed;

    private bool _trueDamageArmed;
    private AttackComponent _attackComponent;
    private MovementComponentRef _movement;
    private Game.Components.Movement.MovementComponent _mc;
    private HitFlash _hitFlash;
    private readonly HashSet<int> _dashHitTargets = new HashSet<int>();
    private readonly HashSet<int> _armoredHitTargets = new HashSet<int>();
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
        _mc = movementComponent;
        _movement = new MovementComponentRef(movementComponent);
        float cooldown = cooldownOverride >= 0f ? cooldownOverride : dashImpactCooldown;
        _attackComponent = new AttackComponent(cooldown);
        _hitFlash = GetComponentInChildren<HitFlash>();
    }

    public void ArmTrueDamage() => _trueDamageArmed = true;

    private void Update()
    {
        if (_movement == null) return;

        bool isDashingNow = _movement.IsDashing;
        if (isDashingNow && !_wasDashing)
        {
            _dashHitTargets.Clear();
            _armoredHitTargets.Clear();
        }

        // Dash just ended — consume the armed buff regardless of whether it hit anything
        if (!isDashingNow && _wasDashing && _trueDamageArmed)
        {
            _trueDamageArmed = false;
            TrueDamageConsumed?.Invoke();
        }

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

        // Boss weakpoint path — boss is not a character, so handled before the character lookup.
        BossWeakPoint bossWeakPoint = targetCollider.GetComponentInParent<BossWeakPoint>();
        if (bossWeakPoint != null)
        {
            int wpId = bossWeakPoint.GetInstanceID();
            if (_dashHitTargets.Contains(wpId)) return;
            _dashHitTargets.Add(wpId);
            if (bossWeakPoint.TryDestroy())
            {
                ImpactLanded?.Invoke();
                SlowMotion.Instance.StartHitstop(hitstopTimeScale, hitstopDuration);
                CameraShake.Shake(weakPointShakeIntensity);
                _hitFlash?.Flash();
                HitFlashFX.Spawn(targetCollider.bounds.center);
                SoundManager.PlaySound(SoundType.HITSTOP);
            }
            return;
        }

        character targetCharacter = targetCollider.GetComponentInParent<character>();
        if (targetCharacter == null || targetCharacter == GetComponentInParent<character>()) return;

        // True Damage armed: kill any regular Enemy on a body hit, bypassing the weak-point requirement.
        bool isTrueDamageKill = _trueDamageArmed && targetCharacter is Enemy;

        bool hitWeakPoint = false;
        if (targetCharacter is Enemy && !_trueDamageArmed)
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

            if (weakPoint == null)
            {
                // Armored hit: bounce only — no damage, no combo. Uses a separate set so a
                // body-collider hit on the same frame cannot block a subsequent weak-point hit.
                int armoredId = targetCharacter.GetInstanceID();
                if (!_armoredHitTargets.Contains(armoredId))
                {
                    _armoredHitTargets.Add(armoredId);
                    _mc?.BounceFromDashImpact(bounceForce, bounceUpwardBias);
                }
                return;
            }
            if (weakPoint.OwnerEnemy != null && weakPoint.OwnerEnemy != targetCharacter) return;
            hitWeakPoint = true;
        }

        int targetId = targetCharacter.GetInstanceID();
        if (_dashHitTargets.Contains(targetId)) return;

        _attackComponent.PerformAttack(targetCharacter, dashImpactBaseDamage);
        _dashHitTargets.Add(targetId);
        ImpactLanded?.Invoke();

        if (hitWeakPoint || isTrueDamageKill)
        {
            SlowMotion.Instance.StartHitstop(hitstopTimeScale, hitstopDuration);
            CameraShake.Shake(weakPointShakeIntensity);
            _hitFlash?.Flash();
            HitFlashFX.Spawn(targetCollider.bounds.center);
            SoundManager.PlaySound(SoundType.HITSTOP);
            targetCharacter.Die();
            StartCoroutine(DashAttackFreezeAndBounce());

            // Consume the armed buff on the first kill so a single dash kills only one enemy.
            if (isTrueDamageKill)
            {
                _trueDamageArmed = false;
                TrueDamageConsumed?.Invoke();
            }
        }
        else
        {
            _mc?.BounceFromDashImpact(bounceForce, bounceUpwardBias);
        }
    }

    private IEnumerator DashAttackFreezeAndBounce()
    {
        _mc?.BeginDashAttackFreeze();
        yield return new WaitForSecondsRealtime(dashAttackFreezeDuration);
        _mc?.BounceFromDashImpact(bounceForce, bounceUpwardBias);
    }
}
