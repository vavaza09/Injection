using UnityEngine;
using System.Collections;

public class FurnaceEnemy : Enemy
{
    [Header("Furnace Ranged Attack")]
    public GameObject projectilePrefab;
    public float spawnHeight = 4f;
    public float fallSpeed = 18f;
    public Vector2 hitboxSize = new Vector2(1.5f, 1.5f);
    public float attackCooldown = 1.25f;
    public float damageAmount = 1f;
    [Tooltip("How long the attack animation plays before the bullet spawns (seconds).")]
    public float attackWindupDuration = 1f;

    [Header("Facing")]
    [Tooltip("Enable if the sprite sheet faces LEFT by default (positive scale = left-facing).")]
    [SerializeField] private bool invertFacing = true;

    public override bool SpriteFacesLeft => invertFacing;

    private float lastAttackTime = -999f;
    private bool _isAttacking;
    private Vector2 _prevPosition;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        _prevPosition = transform.position;
    }

    protected override void Update()
    {
        base.Update();
        if (_isAttacking || _animator == null) return;

        bool isMoving = ((Vector2)transform.position - _prevPosition).sqrMagnitude > 0.00001f;
        _prevPosition = transform.position;

        if (!isMoving
            && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Oven_Idle")
            && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Oven_Attack"))
            _animator.Play("Oven_Idle");
    }

    public override void Patrol(Vector2 direction, float speed)
    {
        if (_animator != null && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Oven_Walk"))
            _animator.Play("Oven_Walk");
        base.Patrol(direction, speed);
    }

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
        if (projectilePrefab == null || playerTransform == null || _isAttacking)
            return;

        if (Time.time < lastAttackTime + attackCooldown)
            return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        lastAttackTime = Time.time;

        if (_animator != null)
            _animator.SetBool("IsAttacking", true);

        yield return new WaitForSeconds(attackWindupDuration);

        Vector3 spawnPosition = playerTransform.position + Vector3.up * spawnHeight;
        GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

        FurnaceProjectile projectile = projectileObject.GetComponent<FurnaceProjectile>();
        if (projectile != null)
            projectile.Initialize(fallSpeed, hitboxSize, damageAmount);

        if (_animator != null)
            _animator.SetBool("IsAttacking", false);

        _isAttacking = false;
    }
}
