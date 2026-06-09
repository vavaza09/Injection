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

    [Header("Barrel")]
    [SerializeField] private float barrelRotateSpeed = 170f;
    [SerializeField] private float barrelMinAngle = -15f;
    [SerializeField] private float barrelMaxAngle = 170f;
    [SerializeField] private float shootMinAngle = -5f;
    [SerializeField] private float shootMaxAngle = 195f;

    private float lastShootTime = -999f;
    private Animator animator;
    private bool canShootAtPlayer;
    private float currentBarrelAngle = 10f;
    private Vector2 lastMoveInput;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();

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
        else
        {
            canShootAtPlayer = false;
            currentBarrelAngle = Mathf.MoveTowards(currentBarrelAngle, 176f, barrelRotateSpeed * Time.deltaTime);
            if (gunPivot != null)
            {
                if (gunPivot.parent != null)
                    gunPivot.localRotation = Quaternion.Euler(0f, 0f, currentBarrelAngle + 185f);
                else
                    gunPivot.rotation = Quaternion.Euler(0f, 0f, currentBarrelAngle + 185f);
            }
        }

        if (GetState() == EnemyState.Chase)
        {
            if (distanceToPlayer > 7.9f)
            {
                Move(Vector2.zero);
                if (animator != null)
                    animator.SetBool("IsWalking", false);
            }
            else
            {
                ChasePlayer();

                bool isMoving = movementComponent != null
                    ? movementComponent.GetVelocity().magnitude > 1f
                    : lastMoveInput.sqrMagnitude > 0.001f;
                if (animator != null)
                    animator.SetBool("IsWalking", isMoving);
            }

            if (distanceToPlayer <= attackRange)
            {
                Attack();
            }
            return;
        }

        Move(Vector2.zero);
        if (animator != null)
            animator.SetBool("IsWalking", false);
    }

    public override void Move(Vector2 direction)
    {
        lastMoveInput = direction;

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

        if (!canShootAtPlayer)
        {
            return;
        }

        if (Time.time < lastShootTime + shootCooldown)
        {
            return;
        }

        Vector2 shootDirection = -attackPoint.right;
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
            float rawAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            float shootAngle = rawAngle < 0f ? rawAngle + 360f : rawAngle;
            float normMin = shootMinAngle < 0f ? shootMinAngle + 360f : shootMinAngle;
            float normMax = shootMaxAngle < 0f ? shootMaxAngle + 360f : shootMaxAngle;
            canShootAtPlayer = normMin <= normMax
                ? shootAngle >= normMin && shootAngle <= normMax
                : shootAngle >= normMin || shootAngle <= normMax;
            bool inVisualArc = rawAngle >= barrelMinAngle && rawAngle <= barrelMaxAngle;
            float targetAngle = inVisualArc ? Mathf.Clamp(rawAngle, barrelMinAngle, barrelMaxAngle) : 176f;
            currentBarrelAngle = Mathf.MoveTowards(currentBarrelAngle, targetAngle, barrelRotateSpeed * Time.deltaTime);
            gunPivot.localRotation = Quaternion.Euler(0f, 0f, currentBarrelAngle + 185f);
        }
        else
        {
            float rawAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float shootAngle = rawAngle < 0f ? rawAngle + 360f : rawAngle;
            float normMin = shootMinAngle < 0f ? shootMinAngle + 360f : shootMinAngle;
            float normMax = shootMaxAngle < 0f ? shootMaxAngle + 360f : shootMaxAngle;
            canShootAtPlayer = normMin <= normMax
                ? shootAngle >= normMin && shootAngle <= normMax
                : shootAngle >= normMin || shootAngle <= normMax;
            bool inVisualArc = rawAngle >= barrelMinAngle && rawAngle <= barrelMaxAngle;
            float targetAngle = inVisualArc ? Mathf.Clamp(rawAngle, barrelMinAngle, barrelMaxAngle) : 176f;
            currentBarrelAngle = Mathf.MoveTowards(currentBarrelAngle, targetAngle, barrelRotateSpeed * Time.deltaTime);
            gunPivot.rotation = Quaternion.Euler(0f, 0f, currentBarrelAngle + 185f);
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
