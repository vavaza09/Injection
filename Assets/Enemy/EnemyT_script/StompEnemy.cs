using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class StompEnemy : Enemy
{
    private Animator anim;

    [Header("Stomp Attack")]
    [SerializeField] private StompDamageZone stompDamageZone;
    [SerializeField] private float preJumpDelay = 0.3f;
    [SerializeField] private float jumpHeightAbovePlayer = 3f;
    [SerializeField] private float jumpToPlayerTime = 0.35f;
    [SerializeField] private float stompDownSpeed = 20f;
    [SerializeField] private float stompDamageDuration = 0.3f;
    [SerializeField] private float stompAttackCooldown = 1.5f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Impact")]
    [SerializeField] private float shakeAmplitude = 1.5f;

    private bool isAttacking;
    private bool isDropping;
    private bool hasGroundImpact;
    private float lastAttackTime = -999f;
    private Coroutine _stompRoutineHandle;
    private Coroutine _damageWindowHandle;

    private Collider2D stomperBodyCollider;
    private Collider2D _playerCollider;
    private CinemachineImpulseSource _impulseSource;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();

        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground");

        if (stompDamageZone == null)
            stompDamageZone = GetComponentInChildren<StompDamageZone>();

        stomperBodyCollider = GetComponent<Collider2D>();

        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null)
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
    }

    protected override void Start()
    {
        base.Start();

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            _playerCollider = playerGO.GetComponent<Collider2D>();
    }

    protected override void Update()
    {
        base.Update();
    }

    public override void PatrolArea()
    {
        if (isAttacking) return;

        if (anim != null)
        {
            anim.SetBool("IsWalking", false);
            if (!anim.GetCurrentAnimatorStateInfo(0).IsName("Crab_Idle")
                && !anim.GetCurrentAnimatorStateInfo(0).IsName("Crab_Attack"))
                anim.Play("Crab_Idle");
        }

        base.PatrolArea();
    }

    public override void Patrol(Vector2 direction, float speed)
    {
        if (anim != null)
        {
            anim.SetBool("IsWalking", true);
            if (!anim.GetCurrentAnimatorStateInfo(0).IsName("Crab_Walk"))
                anim.Play("Crab_Walk");
        }
        base.Patrol(direction, speed);
    }

    public override void Move(Vector2 direction)
    {
        if (isAttacking)
        {
            if (movementComponent != null)
                movementComponent.Stop();
            return;
        }

        base.Move(direction);
    }

    public override void ChasePlayer()
    {
        if (isAttacking)
        {
            Move(Vector2.zero);
            return;
        }

        if (anim != null)
        {
            anim.SetBool("IsWalking", true);
            if (!anim.GetCurrentAnimatorStateInfo(0).IsName("Crab_Walk"))
                anim.Play("Crab_Walk");
        }

        base.ChasePlayer();
    }

    public override void Attack()
    {
        if (playerTransform == null || isAttacking)
            return;

        if (Time.time < lastAttackTime + stompAttackCooldown)
            return;

        _stompRoutineHandle = StartCoroutine(StompAttackRoutine());
    }

    protected override void OnStunInterrupt()
    {
        if (_stompRoutineHandle != null)
        {
            StopCoroutine(_stompRoutineHandle);
            _stompRoutineHandle = null;
        }
        if (_damageWindowHandle != null)
        {
            StopCoroutine(_damageWindowHandle);
            _damageWindowHandle = null;
        }

        stompDamageZone?.DisableZone();
        RestoreCollision();

        if (movementComponent != null)
            movementComponent.SetCanMove(true);

        isAttacking = false;
        isDropping = false;
    }

    private IEnumerator StompAttackRoutine()
    {
        isAttacking = true;
        isDropping = false;
        hasGroundImpact = false;
        lastAttackTime = Time.time;

        if (anim != null)
        {
            anim.SetBool("IsWalking", false);
            anim.Play("Crab_Attack");
        }

        yield return new WaitForSeconds(preJumpDelay);

        if (movementComponent != null)
        {
            movementComponent.SetCanMove(false);
            movementComponent.Stop();
        }

        Vector3 startPosition = transform.position;
        Vector3 targetAbovePlayer = new Vector3(
            playerTransform.position.x,
            playerTransform.position.y + jumpHeightAbovePlayer,
            transform.position.z);

        DisableCollisionWithPlayer();

        float timer = 0f;
        while (timer < jumpToPlayerTime)
        {
            timer += Time.deltaTime;
            float t = jumpToPlayerTime > 0f ? Mathf.Clamp01(timer / jumpToPlayerTime) : 1f;
            transform.position = Vector3.Lerp(startPosition, targetAbovePlayer, t);
            yield return null;
        }

        isDropping = true;
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, -Mathf.Abs(stompDownSpeed));

        while (!hasGroundImpact)
        {
            if (HasLanded())
            {
                hasGroundImpact = true;
                break;
            }
            yield return null;
        }

        isDropping = false;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(shakeAmplitude);

        _damageWindowHandle = StartCoroutine(StompDamageWindow());
        yield return _damageWindowHandle;

        yield return StartCoroutine(WaitUntilClear());
        RestoreCollision();

        if (movementComponent != null)
            movementComponent.SetCanMove(true);

        isAttacking = false;
    }

    private IEnumerator StompDamageWindow()
    {
        stompDamageZone?.EnableZone();
        yield return new WaitForSeconds(stompDamageDuration);
        stompDamageZone?.DisableZone();
    }

    private bool HasLanded()
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(0f, -0.9f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.2f, groundLayer);
        return hit.collider != null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
            TryMarkGroundImpact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryMarkGroundImpact(other);
    }

    private void TryMarkGroundImpact(Collider2D other)
    {
        if (!isDropping || other == null) return;
        if (IsGroundLayer(other.gameObject.layer))
            hasGroundImpact = true;
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundLayer.value & (1 << layer)) != 0;
    }

    private void DisableCollisionWithPlayer()
    {
        if (stomperBodyCollider != null && _playerCollider != null)
            Physics2D.IgnoreCollision(stomperBodyCollider, _playerCollider, true);
    }

    private void RestoreCollision()
    {
        if (stomperBodyCollider != null && _playerCollider != null)
            Physics2D.IgnoreCollision(stomperBodyCollider, _playerCollider, false);
    }

    private IEnumerator WaitUntilClear()
    {
        if (stomperBodyCollider == null || _playerCollider == null) yield break;

        int timeout = 0;
        while (Physics2D.IsTouching(stomperBodyCollider, _playerCollider))
        {
            if (++timeout > 120) break;
            yield return null;
        }

        yield return null;
        yield return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!isDropping) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 1.1f);
    }
}
