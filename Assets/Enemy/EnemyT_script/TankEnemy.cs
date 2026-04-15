using UnityEngine;

public class TankEnemy : Enemy
{
    [Header("Tank Weapon")]
    [SerializeField] private Transform gunPivot;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float shootCooldown = 1f;
    [SerializeField] private float bulletSpeed = 12f;

    [Header("Tank State")]
    [SerializeField] private bool rotatePivotWhenIdle = true;
    [SerializeField] private float stopMoveThreshold = 0.1f;
    [SerializeField] private float backOffRange = 1.25f;

    private float lastShootTime = -999f;

    protected override void Start()
    {
        base.Start();

        if (movementComponent != null)
        {
            movementComponent.SetCanMove(true);
            movementComponent.SetSpeed(moveSpeed);
            movementComponent.Stop();
        }

        SetState(EnemyState.Idle);
    }

    protected override void Update()
    {
        base.Update();

        if (!isAlive)
        {
            return;
        }

        if (playerTransform == null)
        {
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        bool inDetectionRange = distanceToPlayer <= detectionRange;
        SetState(inDetectionRange ? EnemyState.Chase : EnemyState.Idle);

        if (inDetectionRange && (rotatePivotWhenIdle || GetState() == EnemyState.Chase))
        {
            RotatePivotToTarget();
        }

        if (GetState() == EnemyState.Chase)
        {
            ChasePlayer();

            if (distanceToPlayer <= attackRange)
            {
                Attack();
            }
            return;
        }

        Move(Vector2.zero);
    }

    public override void Move(Vector2 direction)
    {
        if (movementComponent != null)
        {
            movementComponent.Move(direction);
        }
        else
        {
            transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
        }
    }

    public override void PatrolArea()
    {
        if (movementComponent != null)
        {
            movementComponent.Stop();
        }
    }

    public override void ChasePlayer()
    {
        if (playerTransform == null)
        {
            Move(Vector2.zero);
            return;
        }

        Vector2 direction = playerTransform.position - transform.position;
        direction.y = 0f;
        float horizontalDistance = Mathf.Abs(direction.x);

        if (horizontalDistance <= backOffRange)
        {
            Move((-direction).normalized);
            return;
        }

        if (horizontalDistance <= stopMoveThreshold)
        {
            Move(Vector2.zero);
            return;
        }

        Move(direction.normalized);
    }

    public override void Attack()
    {
        if (bulletPrefab == null || attackPoint == null || playerTransform == null)
        {
            return;
        }

        if (Time.time < lastShootTime + shootCooldown)
        {
            return;
        }

        Vector2 shootDirection = attackPoint.right;
        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            shootDirection = (playerTransform.position - attackPoint.position).normalized;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector2.right, shootDirection);
        GameObject bulletObject = Instantiate(bulletPrefab, attackPoint.position, rotation);

        TankBullet bullet = bulletObject.GetComponent<TankBullet>();
        if (bullet != null)
        {
            bullet.Initialize(shootDirection, attackDamage, bulletSpeed, gameObject);
        }

        lastShootTime = Time.time;
    }

    private void RotatePivotToTarget()
    {
        if (gunPivot == null || playerTransform == null)
        {
            return;
        }

        Vector2 direction = playerTransform.position - gunPivot.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (gunPivot.parent != null)
        {
            Vector3 localTarget = gunPivot.parent.InverseTransformPoint(playerTransform.position);
            Vector3 localPivot = gunPivot.parent.InverseTransformPoint(gunPivot.position);
            Vector2 localDirection = localTarget - localPivot;
            float localAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            gunPivot.localRotation = Quaternion.Euler(0f, 0f, localAngle);
        }
        else
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            gunPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, backOffRange);

        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, 0.12f);
        }
    }
}
