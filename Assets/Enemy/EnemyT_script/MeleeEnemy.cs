using UnityEngine;

public class MeleeEnemy : Enemy
{
    // Components
    private MeleeEnemyMovement enemyMovement;
    private MeleeEnemyAttack enemyAttack;

    protected override void Start()
    {
        base.Start();

        // Get components
        enemyMovement = GetComponent<MeleeEnemyMovement>();
        enemyAttack = GetComponent<MeleeEnemyAttack>();

        // Validate components
        if (enemyMovement == null)
        {
            Debug.LogError($"MeleeEnemy '{gameObject.name}' is missing MeleeEnemyMovement component!");
        }

        if (enemyAttack == null)
        {
            Debug.LogError($"MeleeEnemy '{gameObject.name}' is missing MeleeEnemyAttack component!");
        }
    }

    private void Update()
    {
        // Update attack state machine
        if (enemyAttack != null)
        {
            enemyAttack.UpdateAttack();
        }
    }

    public override void Move(Vector2 direction)
    {
        // This method is kept for base class compatibility
        // but actual movement is handled by MeleeEnemyMovement
        if (enemyMovement != null && !enemyAttack.IsAttacking)
        {
            enemyMovement.MoveInDirection(direction);
        }
    }

    public override void PatrolArea()
    {
        if (enemyMovement != null && !enemyAttack.IsAttacking)
        {
            enemyMovement.Patrol();
        }
    }

    public override void ChasePlayer()
    {
        if (playerTransform == null) return;

        if (enemyMovement != null && !enemyAttack.IsAttacking)
        {
            enemyMovement.ChaseTarget(playerTransform.position);
        }
    }

    public override void Attack()
    {
        if (playerTransform == null || enemyAttack == null) return;

        // Try jump attack first (if enabled and conditions are met)
        enemyAttack.TryJumpAttack(playerTransform);

        // Try regular attack
        enemyAttack.TryAttack(playerTransform);
    }
}