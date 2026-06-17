using UnityEngine;
using System.Collections;

public class KikiEnemy : Enemy
{
    [Header("Flying Patrol")]
    [SerializeField] private float patrolRadius = 3f;
    [SerializeField] private float floatSpeed = 2.5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.6f;
    [SerializeField] private float waypointReachDist = 0.4f;

    [Header("Headbutt Attack")]
    [SerializeField] private float headbuttLaunchDelay = 0.5f;
    [SerializeField] private int   attackFreezeFrame  = 8;
    [SerializeField] private int   attackTotalFrames  = 14;
    [SerializeField] private float headbuttSpeed      = 14f;
    [SerializeField] private float headbuttDuration   = 0.35f;
    [SerializeField] private float headbuttHitRadius  = 0.6f;
    [SerializeField] private float returnSpeed        = 6f;
    [SerializeField] private float headbuttCooldown   = 1.5f;

    [Header("Facing")]
    [SerializeField] private bool invertFacing = true;

    private Vector2 _spawnCenter;
    private Vector2 _waypoint;
    private bool _isAttacking;
    private bool _hasDamaged;
    private float _lastAttackTime = -999f;
    private Vector2 _preAttackPos;
    private Coroutine _attackCoroutine;
    private Animator _anim;

    protected override void Awake()
    {
        base.Awake();
        _spawnCenter = transform.position;
        _anim = GetComponentInChildren<Animator>();
    }

    protected override void Start()
    {
        base.Start();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
        PickWaypoint();
    }

    protected override void Update()
    {
        base.Update();
        if (!isAlive || IsStunned || _isAttacking) return;

        DetectPlayer();

        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol:
                FloatPatrol();
                break;
            case EnemyState.Chase:
                FloatChase();
                break;
            case EnemyState.Attack:
                TryStartHeadbutt();
                break;
        }
    }

    private void FloatPatrol()
    {
        if (rb == null) return;
        PlayAnim("Float_Idle");

        Vector2 toWaypoint = _waypoint - (Vector2)transform.position;
        if (toWaypoint.magnitude < waypointReachDist)
        {
            rb.linearVelocity = Vector2.zero;
            PickWaypoint();
            return;
        }

        Vector2 dir = toWaypoint.normalized;
        rb.linearVelocity = dir * floatSpeed;
        FlipTo(dir.x > 0);
    }

    private void FloatChase()
    {
        if (rb == null || playerTransform == null) return;
        PlayAnim("Float_Idle");

        Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * (floatSpeed * chaseSpeedMultiplier);
        FlipTo(dir.x > 0);
    }

    private void TryStartHeadbutt()
    {
        if (Time.time < _lastAttackTime + headbuttCooldown) return;
        _attackCoroutine = StartCoroutine(HeadbuttRoutine());
    }

    private IEnumerator HeadbuttRoutine()
    {
        _isAttacking = true;
        _hasDamaged = false;
        _lastAttackTime = Time.time;
        _preAttackPos = transform.position;

        if (rb != null) rb.linearVelocity = Vector2.zero;
        PlayAnim("Float_Attack");

        yield return new WaitForSeconds(headbuttLaunchDelay);

        // Wait until the animation reaches the freeze frame
        float freezeNormalized = (float)attackFreezeFrame / attackTotalFrames;
        yield return new WaitUntil(() =>
        {
            if (_anim == null) return true;
            var info = _anim.GetCurrentAnimatorStateInfo(0);
            return !info.IsName("Float_Attack") || info.normalizedTime >= freezeNormalized;
        });

        // Freeze the animator on that frame
        if (_anim != null) _anim.speed = 0f;

        // Lock in player position and launch
        Vector2 target = playerTransform != null ? (Vector2)playerTransform.position : _preAttackPos;
        Vector2 dashDir = target - (Vector2)transform.position;
        if (dashDir.sqrMagnitude < 0.001f) dashDir = Vector2.right;
        dashDir = dashDir.normalized;
        FlipTo(dashDir.x > 0);

        // Dash — animator stays frozen on frame 8 until a hit registers
        float elapsed = 0f;
        while (elapsed < headbuttDuration)
        {
            elapsed += Time.deltaTime;
            if (rb != null) rb.linearVelocity = dashDir * headbuttSpeed;
            CheckHeadbuttHit();
            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Unfreeze and let the rest of the animation play
        if (_anim != null) _anim.speed = 1f;

        yield return new WaitUntil(() =>
        {
            if (_anim == null) return true;
            var info = _anim.GetCurrentAnimatorStateInfo(0);
            return !info.IsName("Float_Attack") || info.normalizedTime >= 1f;
        });

        PlayAnim("Float_Idle");
        while (Vector2.Distance(transform.position, _preAttackPos) > 0.25f)
        {
            if (rb == null) break;
            Vector2 back = ((Vector2)_preAttackPos - (Vector2)transform.position).normalized;
            rb.linearVelocity = back * returnSpeed;
            yield return null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = _preAttackPos;
        }

        _isAttacking = false;
        PickWaypoint();
        SetState(EnemyState.Idle);
    }

    private void CheckHeadbuttHit()
    {
        if (_hasDamaged) return;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, headbuttHitRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.transform.root == transform) continue;
            character ch = hit.GetComponentInParent<character>();
            if (ch is Player)
            {
                ch.TakeDamage(attackDamage);
                _hasDamaged = true;
                if (_anim != null) _anim.speed = 1f;
                return;
            }
        }
    }

    protected override void OnStunInterrupt()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        _isAttacking = false;
        if (_anim != null) _anim.speed = 1f;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        PlayAnim("Float_Idle");
        PickWaypoint();
    }

    private void PickWaypoint()
    {
        _waypoint = _spawnCenter + Random.insideUnitCircle * patrolRadius;
    }

    private void PlayAnim(string stateName)
    {
        if (_anim == null) return;
        if (!_anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            _anim.Play(stateName);
    }

    private void FlipTo(bool faceRight)
    {
        Vector3 scale = transform.localScale;
        float abs = Mathf.Abs(scale.x);
        scale.x = (faceRight != invertFacing) ? abs : -abs;
        transform.localScale = scale;
    }
}
