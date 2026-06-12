using UnityEngine;
using System.Collections;

public class KikiEnemy : Enemy
{
    [Header("Kiki Smash Attack")]
    public float windUpAngle = 30f;
    public float windUpTime = 0.25f;
    public float smashAngle = 90f;
    public float smashSpeed = 18f;
    public float returnSpeed = 6f;
    public float cooldown = 1.2f;
    public float smashPushForce = 6f;

    private bool isAttacking;
    private bool hasDamagedThisAttack;
    private float lastAttackTime = -999f;
    private Quaternion baseLocalRotation;
    private Coroutine _smashRoutineHandle;

    protected override void Awake()
    {
        base.Awake();
        baseLocalRotation = transform.localRotation;
    }

    public override void ChasePlayer()
    {
        if (playerTransform == null)
        {
            Move(Vector2.zero);
            return;
        }

        if (isAttacking)
        {
            Move(Vector2.zero);
            return;
        }

        Vector2 direction = playerTransform.position - transform.position;
        direction.y = 0f;

        Move(direction.normalized);
    }

    public override void Attack()
    {
        if (playerTransform == null)
        {
            return;
        }

        if (isAttacking)
        {
            return;
        }

        if (Time.time < lastAttackTime + cooldown)
        {
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > attackRange)
        {
            return;
        }

        _smashRoutineHandle = StartCoroutine(SmashAttackRoutine());
    }

    public override void Move(Vector2 direction)
    {
        if (isAttacking)
        {
            if (movementComponent != null)
            {
                movementComponent.Stop();
            }
            return;
        }

        base.Move(direction);
    }

    protected override void OnStunInterrupt()
    {
        if (_smashRoutineHandle != null)
        {
            StopCoroutine(_smashRoutineHandle);
            _smashRoutineHandle = null;
        }
        transform.localRotation = baseLocalRotation;
        isAttacking = false;
    }

    private IEnumerator SmashAttackRoutine()
    {
        isAttacking = true;
        hasDamagedThisAttack = false;
        lastAttackTime = Time.time;

        if (movementComponent != null)
        {
            movementComponent.Stop();
        }

        float playerSide = 1f;
        if (playerTransform != null)
        {
            float deltaX = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) > 0.001f)
            {
                playerSide = Mathf.Sign(deltaX);
            }
        }

        float signedWindUpAngle = playerSide * Mathf.Abs(windUpAngle);
        float signedSmashAngle = -playerSide * Mathf.Abs(smashAngle);

        Quaternion windUpTarget = baseLocalRotation * Quaternion.Euler(0f, 0f, signedWindUpAngle);
        Quaternion smashTarget = baseLocalRotation * Quaternion.Euler(0f, 0f, signedSmashAngle);

        float windTimer = 0f;
        while (windTimer < windUpTime)
        {
            windTimer += Time.deltaTime;
            float t = windUpTime > 0f ? Mathf.Clamp01(windTimer / windUpTime) : 1f;
            transform.localRotation = Quaternion.Lerp(baseLocalRotation, windUpTarget, t);
            yield return null;
        }

        PushToPlayer();

        while (Quaternion.Angle(transform.localRotation, smashTarget) > 0.5f)
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, smashTarget, smashSpeed * Time.deltaTime);
            yield return null;
        }

        while (Quaternion.Angle(transform.localRotation, baseLocalRotation) > 0.5f)
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, baseLocalRotation, returnSpeed * Time.deltaTime);
            yield return null;
        }

        transform.localRotation = baseLocalRotation;

        isAttacking = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TrySmashDamage(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrySmashDamage(other);
    }

    private void TrySmashDamage(Collider2D targetCollider)
    {
        if (!isAttacking)
        {
            return;
        }

        if (hasDamagedThisAttack)
        {
            return;
        }

        if (targetCollider == null)
        {
            return;
        }

        character targetCharacter = targetCollider.GetComponentInParent<character>();
        if (targetCharacter == null)
        {
            return;
        }

        if (!(targetCharacter is Player))
        {
            return;
        }

        targetCharacter.TakeDamage(attackDamage);
        hasDamagedThisAttack = true;
    }

    private void PushToPlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector2 pushDirection = playerTransform.position - transform.position;

        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (rb != null)
        {
            rb.AddForce(pushDirection.normalized * smashPushForce, ForceMode2D.Impulse);
        }
    }
}
