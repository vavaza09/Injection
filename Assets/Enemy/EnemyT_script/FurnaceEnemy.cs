using UnityEngine;

public class FurnaceEnemy : Enemy
{
    [Header("Furnace Ranged Attack")]
    public GameObject projectilePrefab;
    public float spawnHeight = 4f;
    public float fallSpeed = 18f;
    public Vector2 hitboxSize = new Vector2(1.5f, 1.5f);
    public float attackCooldown = 1.25f;
    public float damageAmount = 1f;

    private float lastAttackTime = -999f;

    public override void ChasePlayer()
    {
        Move(Vector2.zero);
    }

    public override void PatrolArea()
    {
        Move(Vector2.zero);
    }

    public override void Move(Vector2 direction)
    {
        if (movementComponent != null)
        {
            movementComponent.Stop();
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    public override void Attack()
    {
        if (projectilePrefab == null)
        {
            return;
        }

        if (playerTransform == null)
        {
            return;
        }

        if (Time.time < lastAttackTime + attackCooldown)
        {
            return;
        }

        Vector3 spawnPosition = playerTransform.position + Vector3.up * spawnHeight;
        GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

        FurnaceProjectile projectile = projectileObject.GetComponent<FurnaceProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(fallSpeed, hitboxSize, damageAmount);
        }

        lastAttackTime = Time.time;
    }
}
