using UnityEngine;

public class Boss : BossBase
{
    [Header("Sprite References")]
    [SerializeField] private Transform upperR;
    [SerializeField] private Transform upperL;
    [SerializeField] private Transform clawR;
    [SerializeField] private Transform bossBody;

    [Header("Attack Joints")]
    [SerializeField] private Transform upperLJoint;   // Upper_L_Joint
    [SerializeField] private Transform lowerLJoint;   // Lower_L_Joint
    [SerializeField] private Transform hammer;        // Boss_Hammer(L)

    [Header("Attack Settings")]
    [SerializeField] private float attackRange    = 5f;
    [SerializeField] private float hammerCooldown = 10f;

    [Header("Thrust Distances (scale up/down if arm over/under-shoots)")]
    [SerializeField] private float upperThrustX = 1.5f;
    [SerializeField] private float lowerThrustX = 1.0f;

    private BossIdleState _idleState;

    private void Start()
    {
        ResolveReferences();
        _idleState = new BossIdleState(this);
        TransitionTo(_idleState);
    }

    public override void OnPlayerDetected()
    {
        var attack = new BossHammerAttack(
            this,
            upperLJoint,
            lowerLJoint,
            hammer,
            upperThrustX,
            lowerThrustX,
            attackRange,
            hammerCooldown,
            OnHammerImpact);

        TransitionTo(new BossAttackState(this, attack));
        Debug.Log("[Boss] Player detected → AttackState");
    }

    public override void OnPlayerLost()
    {
        TransitionTo(_idleState);
        Debug.Log("[Boss] Player lost → IdleState");
    }

    public override void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth  = Mathf.Max(0, currentHealth);
        if (currentHealth <= 0) HandleDeath();
    }

    public void Exit()
    {
        currentState?.Exit();
        StopAllCoroutines();
    }

    private void OnHammerImpact()
    {
        // TODO: spawn hitbox, trigger camera shake, play VFX/SFX here
    }

    private void HandleDeath()
    {
        Exit();
        Destroy(gameObject, 1f);
    }

    private void ResolveReferences()
    {
        if (upperLJoint == null) upperLJoint = FindDeep("Upper_L_Joint");
        if (lowerLJoint == null) lowerLJoint = FindDeep("Lower_L_Joint");
        if (hammer      == null) hammer      = FindDeep("Boss_Hammer(L)");
        if (upperR      == null) upperR      = FindDeep("Boss_Upper_R");
        if (upperL      == null) upperL      = FindDeep("Boss_Upper_L");
        if (clawR       == null) clawR       = FindDeep("Boss_Claw(R)");
        if (bossBody    == null) bossBody    = FindDeep("Boss_Body");

        if (upperLJoint == null) Debug.LogWarning("[Boss] Upper_L_Joint not found.");
        if (lowerLJoint == null) Debug.LogWarning("[Boss] Lower_L_Joint not found.");
        if (hammer      == null) Debug.LogWarning("[Boss] Boss_Hammer(L) not found.");
    }

    private Transform FindDeep(string childName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }
}
