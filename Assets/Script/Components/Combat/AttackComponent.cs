public class AttackComponent
{
    public float AttackDamage { get; private set; }
    public float AttackCooldown { get; private set; }
    public float LastAttackTime { get; private set; }

    public void PerformAttack()
    { 
        // Implement attack logic here (e.g., deal damage to target)
    }

    public bool CanAttack()
    {
        return false;
    }

    public void SetDamage(float damage)
    {

    }
}
