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

    [Header("Double Shot")]
    [SerializeField] private MuzzleFlashEffect muzzleFlash;

    [Header("Facing")]
    [Tooltip("Enable if the sprite sheet faces LEFT by default (positive scale = left-facing).")]
    [SerializeField] private bool invertFacing = true;

    public override bool SpriteFacesLeft => invertFacing;

    private float lastAttackTime = -999f;
    private bool _isAttacking;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
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
        if (_animator != null
            && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Oven_Idle")
            && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Oven_Attack"))
            _animator.Play("Oven_Idle");
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

        // Start flash just before the shot — keeps smoke aligned with bullet spawn
        const float flashPreDelay = 0.15f;
        float preWait = Mathf.Max(0f, attackWindupDuration - flashPreDelay);
        yield return new WaitForSeconds(preWait);
        if (muzzleFlash != null)
            muzzleFlash.Play(Quaternion.Euler(0f, 0f, 90f));
        yield return new WaitForSeconds(attackWindupDuration - preWait);

        Vector2 playerPos = playerTransform.position;

        // First projectile — smoke already rolling
        SpawnProjectile(playerPos + new Vector2(-0.5f, 0f));

        yield return new WaitForSeconds(0.1f);

        // Second projectile — restart flash+smoke cycle for the follow-up shot
        if (muzzleFlash != null)
            muzzleFlash.Play(Quaternion.Euler(0f, 0f, 90f));
        SpawnProjectile(playerPos + new Vector2(0.5f, 0f));

        if (_animator != null)
            _animator.SetBool("IsAttacking", false);

        _isAttacking = false;
    }

    private void SpawnProjectile(Vector2 targetPos)
    {
        Vector3 spawnPosition = new Vector3(targetPos.x, targetPos.y + spawnHeight, 0f);
        GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        FurnaceProjectile projectile = projectileObject.GetComponent<FurnaceProjectile>();
        if (projectile != null)
            projectile.Initialize(fallSpeed, hitboxSize, damageAmount);
    }
}
