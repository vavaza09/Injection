using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Abstract base for all boss attacks. Handles cooldown timing and exposes a
/// uniform Execute() entry point so the BossController doesn't care which
/// concrete attack it is running.
/// </summary>
public abstract class BossAttack : MonoBehaviour
{
    [Header("Cooldown")]
    [Tooltip("Seconds before this attack can be used again after it finishes.")]
    [SerializeField] protected float cooldown = 10f;

    [Header("Hooks")]
    [Tooltip("Invoked at the moment of impact. Wire up VFX / SFX / damage here.")]
    public UnityEvent OnImpact;

    private float _cooldownEndTime = -999f;
    public bool IsAttacking { get; protected set; }

    /// <summary>True when not attacking and the cooldown has elapsed.</summary>
    public bool CanAttack => !IsAttacking && Time.time >= _cooldownEndTime;

    /// <summary>Seconds left on cooldown (0 if ready). Handy for UI/debug.</summary>
    public float CooldownRemaining => Mathf.Max(0f, _cooldownEndTime - Time.time);

    /// <summary>Kick off the attack. No-op if it can't currently attack.</summary>
    public void Execute()
    {
        if (!CanAttack) return;
        StartCoroutine(RunAttack());
    }

    /// <summary>Concrete attacks implement their own sequence here.</summary>
    protected abstract IEnumerator AttackRoutine();

    private IEnumerator RunAttack()
    {
        IsAttacking = true;
        yield return AttackRoutine();
        IsAttacking = false;
        StartCooldown();
    }

    protected void StartCooldown() => _cooldownEndTime = Time.time + cooldown;

    /// <summary>Fire the impact hook from concrete attacks at the right frame.</summary>
    protected void RaiseImpact() => OnImpact?.Invoke();
}
