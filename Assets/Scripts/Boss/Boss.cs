using UnityEngine;

public class Boss : BossBase
{
    [Header("Arm References")]
    [SerializeField] private Transform upperR;
    [SerializeField] private Transform upperL;
    [SerializeField] private Transform clawR;
    [SerializeField] private Transform hammerL;
    [SerializeField] private Transform bossBody;

    [Header("Attack")]
    [SerializeField] private GameObject hammerHitboxPrefab;
    [SerializeField] private float hammerAttackInterval = 2f;

    private BossIdleState _idleState;

    private void Start()
    {
        _idleState = new BossIdleState(this);
        TransitionTo(_idleState);
    }

    public override void OnPlayerDetected()
    {
        if (hammerHitboxPrefab == null)
            Debug.LogWarning("[Boss] hammerHitboxPrefab is not assigned in Inspector — attack will spawn nothing.");

        TransitionTo(new BossAttackState(this,
            new BossHammerAttack(this, hammerHitboxPrefab, upperL, hammerL, hammerAttackInterval)));

        Debug.Log("[Boss] Player detected → entering AttackState");
    }

    public override void OnPlayerLost()
    {
        TransitionTo(_idleState);
        Debug.Log("[Boss] Player lost → returning to IdleState");
    }

    public override void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        if (currentHealth <= 0) HandleDeath();
    }

    public void Exit()
    {
        currentState?.Exit();
        StopAllCoroutines();
    }

    private void HandleDeath()
    {
        Exit();
        Destroy(gameObject, 1f);
    }
}
