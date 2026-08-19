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

    [Header("Enemy Detection")]
    [Tooltip("Radius of the swept circle cast along the dash path (match player capsule half-width).")]
    [SerializeField] private float dashCastRadius = 0.5f;
    [SerializeField] private LayerMask enemyDetectionLayer;

    [Header("Hitstop")]
    [SerializeField] private float hitstopDuration = 1f;
    [SerializeField] private float hitstopTimeScale = 0.2f;
    [SerializeField] private float dashAttackFreezeDuration = 0.2f;

    [Header("Weak Point Hit Feedback")]
    [SerializeField] private float weakPointShakeIntensity = 0.1f;
    [SerializeField] private GameObject weakPointHitVfxPrefab;
    [SerializeField] private float weakPointHitVfxLifetime = 1f;
    [Tooltip("Forced sorting order for the VFX's particle renderers so they draw in front of the player sprite.")]
    [SerializeField] private int weakPointHitVfxSortingOrder = 10;

    [Header("Bounce on Hit")]
    [SerializeField] private float bounceForceH = 80f;
    [SerializeField] private float bounceForceV = 200f;
    [SerializeField] private float bounceUpwardBias = 0.2f;

    public event Action ImpactLanded;
    public event Action TrueDamageConsumed;

    private bool _trueDamageArmed;
    private AttackComponent _attackComponent;
    private MovementComponentRef _movement;
    private Game.Components.Movement.MovementComponent _mc;
    private HitFlash _hitFlash;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;
    private readonly HashSet<int> _dashHitTargets = new HashSet<int>();
    private bool _wasDashing;
    private Vector2 _prevPosition;
    private ContactFilter2D _enemyFilter;
    private readonly List<RaycastHit2D> _castResults = new List<RaycastHit2D>(16);

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
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();

        if (enemyDetectionLayer.value == 0)
            enemyDetectionLayer = LayerMask.GetMask("Enemy");

        _enemyFilter = new ContactFilter2D();
        _enemyFilter.useLayerMask = true;
        _enemyFilter.layerMask = enemyDetectionLayer;
        _enemyFilter.useTriggers = true;

        _prevPosition = _rb != null ? _rb.position : (Vector2)transform.position;
    }

    public void ArmTrueDamage() => _trueDamageArmed = true;

    private void Update()
    {
        if (_movement == null) return;

        bool isDashingNow = _movement.IsDashing;
        if (isDashingNow && !_wasDashing)
            _dashHitTargets.Clear();

        // Dash just ended — consume the armed buff regardless of whether it hit anything
        if (!isDashingNow && _wasDashing && _trueDamageArmed)
        {
            _trueDamageArmed = false;
            TrueDamageConsumed?.Invoke();
        }

        _wasDashing = isDashingNow;
    }

    // Boss weak-point path: bosses stay on Default layer so physics callbacks still fire.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleBossWeakPoint(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleBossWeakPoint(other);
    }

    private void TryHandleBossWeakPoint(Collider2D targetCollider)
    {
        if (_movement == null || targetCollider == null) return;
        if (!_movement.IsDashing || !_movement.DashAttacking) return;
        if (_attackComponent == null || !_attackComponent.CanAttack()) return;

        BossWeakPoint bossWeakPoint = targetCollider.GetComponentInParent<BossWeakPoint>();
        if (bossWeakPoint == null) return;

        int wpId = bossWeakPoint.GetInstanceID();
        if (_dashHitTargets.Contains(wpId)) return;
        _dashHitTargets.Add(wpId);
        if (bossWeakPoint.TryDestroy())
        {
            ImpactLanded?.Invoke();
            SlowMotion.Instance.StartHitstop(hitstopTimeScale, hitstopDuration);
            CameraShake.Shake(weakPointShakeIntensity);
            _hitFlash?.Flash();
            SpawnWeakPointHitVfx(targetCollider.bounds.center);
            SoundManager.PlaySound(SoundType.HITSTOP);
        }
    }

    private void FixedUpdate()
    {
        Vector2 currentPos = _rb != null ? _rb.position : (Vector2)transform.position;

        if (_movement == null || !_movement.IsDashing || !_movement.DashAttacking)
        {
            _prevPosition = currentPos;
            return;
        }

        if (_attackComponent == null || !_attackComponent.CanAttack())
        {
            _prevPosition = currentPos;
            return;
        }

        Vector2 delta = currentPos - _prevPosition;
        float distance = delta.magnitude;

        if (distance > 0.001f)
        {
            _castResults.Clear();
            int count = Physics2D.CircleCast(_prevPosition, dashCastRadius, delta.normalized, _enemyFilter, _castResults, distance);

            // Hits are sorted near→far by CircleCast; nearest collider wins.
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _castResults[i];
                if (hit.collider == null) continue;

                character targetCharacter = hit.collider.GetComponentInParent<character>();
                if (targetCharacter == null || targetCharacter == GetComponentInParent<character>()) continue;
                if (!(targetCharacter is Enemy)) continue;

                int targetId = targetCharacter.GetInstanceID();
                if (_dashHitTargets.Contains(targetId)) continue;

                _dashHitTargets.Add(targetId);

                if (_trueDamageArmed)
                {
                    // True damage is unblockable — kills on any enemy collider, ignoring armor.
                    _attackComponent.PerformAttack(targetCharacter, dashImpactBaseDamage);
                    ImpactLanded?.Invoke();
                    SlowMotion.Instance.StartHitstop(hitstopTimeScale, hitstopDuration);
                    CameraShake.Shake(weakPointShakeIntensity);
                    _hitFlash?.Flash();
                    SpawnWeakPointHitVfx(hit.point);
                    SoundManager.PlaySound(SoundType.HITSTOP);
                    targetCharacter.Die();
                    StartCoroutine(DashAttackFreezeAndBounce());
                    _trueDamageArmed = false;
                    TrueDamageConsumed?.Invoke();
                    break;
                }

                // Normal dash: nearest collider determines outcome.
                // If it's a weak point → kill; if it's armor → bounce and shield everything behind it.
                if (ResolveIsWeakPoint(hit.collider, targetCharacter))
                {
                    _attackComponent.PerformAttack(targetCharacter, dashImpactBaseDamage);
                    ImpactLanded?.Invoke();
                    SlowMotion.Instance.StartHitstop(hitstopTimeScale, hitstopDuration);
                    CameraShake.Shake(weakPointShakeIntensity);
                    _hitFlash?.Flash();
                    SpawnWeakPointHitVfx(hit.point);
                    SoundManager.PlaySound(SoundType.HITSTOP);
                    targetCharacter.Die();
                    StartCoroutine(DashAttackFreezeAndBounce());
                }
                else
                {
                    // Armor hit: bounce, no damage; weak point behind is shielded for this dash.
                    _mc?.BounceFromDashImpact(bounceForceH, bounceForceV, bounceUpwardBias);
                }
                break;
            }
        }

        _prevPosition = currentPos;
    }

    private void SpawnWeakPointHitVfx(Vector3 pos)
    {
        ExplosionFlashLight2D.Spawn(pos, outerRadius: 9f, color: new Color(0.3f, 0.6f, 1f));

        if (weakPointHitVfxPrefab == null)
        {
            HitFlashFX.Spawn(pos);
            return;
        }
        GameObject fx = Instantiate(weakPointHitVfxPrefab, pos, Quaternion.identity);
        int layerID = _spriteRenderer != null ? _spriteRenderer.sortingLayerID : 0;
        foreach (ParticleSystemRenderer renderer in fx.GetComponentsInChildren<ParticleSystemRenderer>())
        {
            renderer.sortingLayerID = layerID; // match the player's actual sorting layer, not just "Default"
            renderer.sortingOrder = weakPointHitVfxSortingOrder;
        }
        Destroy(fx, weakPointHitVfxLifetime);
    }

    private bool ResolveIsWeakPoint(Collider2D hitCollider, character targetCharacter)
    {
        EnemyWeakPoint weakPoint = hitCollider.GetComponent<EnemyWeakPoint>();
        if (weakPoint == null)
        {
            EnemyWeakPoint[] weakPoints = targetCharacter.GetComponentsInChildren<EnemyWeakPoint>();
            for (int i = 0; i < weakPoints.Length; i++)
            {
                if (weakPoints[i] != null && weakPoints[i].IsWeakPoint(hitCollider))
                {
                    weakPoint = weakPoints[i];
                    break;
                }
            }
        }
        if (weakPoint == null) return false;
        if (weakPoint.OwnerEnemy != null && weakPoint.OwnerEnemy != targetCharacter) return false;
        return true;
    }

    private IEnumerator DashAttackFreezeAndBounce()
    {
        _mc?.BeginDashAttackFreeze();
        yield return new WaitForSecondsRealtime(dashAttackFreezeDuration);
        yield return new WaitUntil(() => !SlowMotion.Instance.IsHitstopActive);
        _mc?.BounceFromDashImpact(bounceForceH, bounceForceV, bounceUpwardBias);
    }
}
