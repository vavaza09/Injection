using UnityEngine;

namespace Game.Components.Combat
{

public class AttackComponent
{
    public float AttackDamage   { get; private set; }
    public float AttackCooldown { get; private set; }
    public float LastAttackTime { get; private set; }

    public AttackComponent(float cooldown)
    {
        AttackCooldown = cooldown;
        LastAttackTime = -cooldown;
    }

    public void SetDamage(float damage)
    {
        AttackDamage = damage;
    }

    public bool CanAttack()
    {
        return Time.time - LastAttackTime >= AttackCooldown;
    }

    public void PerformAttack(character target, float damage)
    {
        if (target == null) return;
        // NOTE: HealthComponent currently treats every hit as 1 regardless of damage value.
        // damage is passed through for when float damage is implemented.
        target.TakeDamage(damage);
        LastAttackTime = Time.time;
    }
}

}
