using UnityEngine;

public class MeleeEnemy : Enemy
{
    [SerializeField] private float meleeRange = 1.2f;

    [SerializeField] private float attackCooldown = 0.8f;
    private float lastAttackTime;
    private int facing = 1;

    public override void Attack()
    {
        if (playerTransform == null) return;

        UpdateFacing();

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= meleeRange && Time.time >= lastAttackTime + attackCooldown)
        {
            PerformMeleeAttack();
            lastAttackTime = Time.time;
        }
    }

    private void PerformMeleeAttack()
    {
        //if (attackComponent != null)
        //{
        //    attackComponent.PerformAttack();
        //}
            
    }

    private void UpdateFacing()
    {
        float dir = playerTransform.position.x - transform.position.x;

        if (dir > 0)
            facing = 1;
        else if (dir < 0)
            facing = -1;

        // Optional: flip sprite by scale
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
    }
}
