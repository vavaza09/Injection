using System.Xml.Linq;
using UnityEngine;

public class AttackComponent
{
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackCooldown = 0.5f;

    private float lastAttackTime = -999f;

    public void PerformAttack()
    {
        if (!CanAttack()) return;

        lastAttackTime = Time.time;
        Debug.Log($"PerformAttack dmg={attackDamage}");
    }

    public bool CanAttack()
    {
        return Time.time >= lastAttackTime + attackCooldown;
    }

    public void SetDamage(float damage)
    {
        attackDamage = damage;
    }
}
