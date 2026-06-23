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
    [SerializeField] private float jumpUpSpeed = 15f;
    [SerializeField] private float jumpToPlayerTime = 0.35f;
    [SerializeField] private float stompDownSpeed = 20f;
    [SerializeField] private float stompDamageDuration = 0.3f;
    [SerializeField] private float stompAttackCooldown = 1.5f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Mid-Air Contact")]
    [SerializeField] private float airContactDamage = 20f;

    [Header("Impact")]
    [SerializeField] private float shakeAmplitude = 1.5f;
    [SerializeField] private float impactVfxRadius = 2f;
    [SerializeField] private StompImpactVFX impactVfxPrefab;

    private enum StompPhase { None, Jump, Fall, Land }
    private StompPhase _stompPhase = StompPhase.None;

    private bool isAttacking;
    private bool hasGroundImpact;
    private bool _airContactHasHit;
    private float lastAttackTime = -999f;
    private Coroutine _stompRoutineHandle;
    private Coroutine _damageWindowHandle;

    private Collider2D stomperBodyCollider;
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
    }

    protected override void Update()
    {
        base.Update();
    }

    private void FixedUpdate()
    {
        if (_stompPhase != StompPhase.Jump && _stompPhase != StompPhase.Fall)
            return;
        if (_airContactHasHit || stomperBodyCollider == null)
            return;

        if (PlayerCollider == null)
            return;

        if (stomperBodyCollider.bounds.Intersects(PlayerCollider.bounds))
        {
            Player player = PlayerCollider.GetComponentInParent<Player>();
            if (player != null)
            {
                player.TakeDamage(airContactDamage);
                _airContactHasHit = true;
            }
        }
    }

    private void SpawnImpactVFX()
    {
        Vector3 impactPos = stomperBodyCollider != null
            ? new Vector3(stomperBodyCollider.bounds.center.x, stomperBodyCollider.bounds.min.y, transform.position.z)
            : transform.position;

        if (impactVfxPrefab != null)
        {
            StompImpactVFX vfx = Instantiate(impactVfxPrefab, impactPos, Quaternion.identity);
            vfx.Initialize(impactVfxRadius);
        }
        else
        {
            GameObject go = new GameObject("StompImpactVFX");
            go.transform.position = impactPos;
            StompImpactVFX vfx = go.AddComponent<StompImpactVFX>();
            vfx.Initialize(impactVfxRadius);
        }
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
        IgnorePlayerCollision(false);
        if (stomperBodyCollider != null)
            stomperBodyCollider.enabled = true;

        if (movementComponent != null)
            movementComponent.SetCanMove(true);

        isAttacking = false;
        _stompPhase = StompPhase.None;
    }

    private IEnumerator StompAttackRoutine()
    {
        isAttacking = true;
        _stompPhase = StompPhase.None;
        hasGroundImpact = false;
        _airContactHasHit = false;
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
        Vector3 apexAboveStart = new Vector3(startPosition.x, targetAbovePlayer.y, startPosition.z);

        IgnorePlayerCollision(true);

        _stompPhase = StompPhase.Jump;
        if (stomperBodyCollider != null)
            stomperBodyCollider.enabled = false;

        // Phase 1: rise straight up at jumpUpSpeed (units/sec)
        float riseDistance = Mathf.Abs(apexAboveStart.y - startPosition.y);
        float riseDuration = jumpUpSpeed > 0f ? riseDistance / jumpUpSpeed : 0f;
        float timer = 0f;
        while (timer < riseDuration)
        {
            timer += Time.deltaTime;
            float t = riseDuration > 0f ? Mathf.Clamp01(timer / riseDuration) : 1f;
            transform.position = Vector3.Lerp(startPosition, apexAboveStart, t);
            yield return null;
        }

        // Phase 2: slide horizontally over the player over jumpToPlayerTime
        timer = 0f;
        while (timer < jumpToPlayerTime)
        {
            timer += Time.deltaTime;
            float t = jumpToPlayerTime > 0f ? Mathf.Clamp01(timer / jumpToPlayerTime) : 1f;
            transform.position = Vector3.Lerp(apexAboveStart, targetAbovePlayer, t);
            yield return null;
        }

        _stompPhase = StompPhase.Fall;
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

        // STOMP_LAND: halt the fall, slam the ground.
        _stompPhase = StompPhase.Land;
        if (stomperBodyCollider != null)
            stomperBodyCollider.enabled = true;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SpawnImpactVFX();

        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(shakeAmplitude);

        _damageWindowHandle = StartCoroutine(StompDamageWindow());
        yield return _damageWindowHandle;

        yield return StartCoroutine(WaitUntilClearOfPlayer());
        IgnorePlayerCollision(false);

        if (movementComponent != null)
            movementComponent.SetCanMove(true);

        isAttacking = false;
        _stompPhase = StompPhase.None;

        if (anim != null)
            anim.Play("Crab_Idle");
        SetState(EnemyState.Idle);
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
        if (_stompPhase != StompPhase.Fall || other == null) return;
        if (IsGroundLayer(other.gameObject.layer))
            hasGroundImpact = true;
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundLayer.value & (1 << layer)) != 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (_stompPhase != StompPhase.Fall) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 1.1f);
    }
}
