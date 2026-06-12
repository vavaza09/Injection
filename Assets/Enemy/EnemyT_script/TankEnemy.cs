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
    [SerializeField] private float flipDelay = 1f;

    [Header("Barrel")]
    [SerializeField] private float barrelRotateSpeed = 170f;
    [SerializeField] private float gunArcDegrees = 60f;

    private float lastShootTime = -999f;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool canShootAtPlayer;
    private float currentBarrelAngle = 10f;
    private Vector2 lastMoveInput;

    private bool _currentFacingRight;
    private bool _pendingFacingRight;
    private float _flipTimer = -1f;

    private Transform _gunMountParent;
    private float _gunPivotDefaultLocalX;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        _currentFacingRight = spriteRenderer != null && spriteRenderer.flipX;

        // Move gunPivot out of the non-uniformly scaled Circle parent so rotation
        // doesn't cause visual width distortion (Circle has scale 0.55 x 1.04).
        if (gunPivot != null && gunPivot.parent != null && gunPivot.parent != transform)
        {
            _gunMountParent = gunPivot.parent;
            gunPivot.SetParent(transform, worldPositionStays: true);
            _gunPivotDefaultLocalX = gunPivot.localPosition.x;
        }

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

        if (IsStunned)
        {
            Move(Vector2.zero);
            if (animator != null)
                animator.SetBool("IsWalking", false);
            canShootAtPlayer = false;
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
            FlipTowardsPlayer();
            RotatePivotToTarget();
        }
        else
        {
            canShootAtPlayer = false;
            float idleAngle = _currentFacingRight ? 0f : 180f;
            currentBarrelAngle = Mathf.MoveTowards(currentBarrelAngle, idleAngle, barrelRotateSpeed * Time.deltaTime);
            if (gunPivot != null)
                gunPivot.localRotation = Quaternion.Euler(0f, 0f, currentBarrelAngle + 185f);
        }

        if (GetState() == EnemyState.Chase)
        {
            if (distanceToPlayer > backOffRange - 0.1f)
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

        Vector2 shootDirection = (playerTransform.position - attackPoint.position).normalized;

        Quaternion rotation = Quaternion.FromToRotation(Vector2.right, shootDirection);
        GameObject bulletObject = Instantiate(bulletPrefab, attackPoint.position, rotation);

        TankBullet bullet = bulletObject.GetComponent<TankBullet>();
        if (bullet != null)
        {
            bullet.Initialize(shootDirection, attackDamage, bulletSpeed, gameObject);
        }

        lastShootTime = Time.time;
    }

    private void FlipTowardsPlayer()
    {
        if (playerTransform == null || spriteRenderer == null) return;
        bool desiredFacingRight = playerTransform.position.x > transform.position.x;

        if (desiredFacingRight == _currentFacingRight)
        {
            _flipTimer = -1f;
            return;
        }

        if (_pendingFacingRight != desiredFacingRight)
        {
            _pendingFacingRight = desiredFacingRight;
            _flipTimer = flipDelay;
        }

        _flipTimer -= Time.deltaTime;
        if (_flipTimer <= 0f)
        {
            _currentFacingRight = desiredFacingRight;
            _flipTimer = -1f;
            ApplyFlip(_currentFacingRight);
        }
    }

    private void ApplyFlip(bool facingRight)
    {
        spriteRenderer.flipX = facingRight;

        // Flip the Circle mount visual
        if (_gunMountParent != null)
        {
            Vector3 s = _gunMountParent.localScale;
            s.x = facingRight ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            _gunMountParent.localScale = s;
        }

        // Mirror the pivot to the correct side — no scale change, so rotation stays undistorted
        if (gunPivot != null)
        {
            Vector3 pos = gunPivot.localPosition;
            pos.x = facingRight ? -_gunPivotDefaultLocalX : _gunPivotDefaultLocalX;
            gunPivot.localPosition = pos;
        }

        // Snap barrel to new facing direction so it doesn't spin 180° on flip
        currentBarrelAngle = facingRight ? 0f : 180f;
    }

    private void RotatePivotToTarget()
    {
        if (gunPivot == null || playerTransform == null) return;

        Vector2 direction = (Vector2)(playerTransform.position - gunPivot.position);
        if (direction.sqrMagnitude <= 0.0001f) return;

        float rawAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float facingAngle = _currentFacingRight ? 0f : 180f;
        float halfArc = gunArcDegrees * 0.5f;

        float angleDiff = Mathf.DeltaAngle(facingAngle, rawAngle);
        canShootAtPlayer = Mathf.Abs(angleDiff) <= halfArc;
        float targetAngle = facingAngle + Mathf.Clamp(angleDiff, -halfArc, halfArc);

        currentBarrelAngle = Mathf.MoveTowards(currentBarrelAngle, targetAngle, barrelRotateSpeed * Time.deltaTime);
        gunPivot.localRotation = Quaternion.Euler(0f, 0f, currentBarrelAngle + 185f);
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
