using UnityEngine;

public class MeleeEnemy : Enemy
{
    private MeleeEnemyMovement movement;
    private MeleeEnemyAttack attack;

    protected override void Start()
    {
        base.Start();
        movement = GetComponent<MeleeEnemyMovement>();
        attack = GetComponent<MeleeEnemyAttack>();
    }

    protected override void Update()
    {
        base.Update();
        if (!isAlive) return;
        if (attack != null)
            attack.UpdateAttack();
    }

    public override void Move(Vector2 direction)
    {
        // Not used
    }

    public override void PatrolArea()
    {
        if (movement != null)
        {
            movement.Patrol();
        }
    }

    public override void ChasePlayer()
    {
        if (playerTransform != null && movement != null)
        {
            movement.ChaseTarget(playerTransform.position);
        }
    }

    public override void Attack()
    {
        if (playerTransform != null && attack != null)
        {
            if (movement != null)
            {
                movement.StopMovement();
            }
            attack.TryAttack(playerTransform);
        }
    }
}