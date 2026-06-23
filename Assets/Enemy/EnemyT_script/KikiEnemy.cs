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

    [Header("Headbutt Trail Effect")]
    [Tooltip("Trail color at the start (near Kiki)")]
    [SerializeField] private Color trailStartColor = new Color(1f, 0.6f, 0.2f, 0.9f);
    [Tooltip("Trail color at the end (fading tail)")]
    [SerializeField] private Color trailEndColor = new Color(1f, 0.3f, 0.05f, 0f);
    [Tooltip("Trail width at start")]
    [SerializeField] private float trailStartWidth = 0.5f;
    [Tooltip("Trail width at end")]
    [SerializeField] private float trailEndWidth = 0.05f;
    [Tooltip("How long the trail lingers behind (seconds)")]
    [SerializeField] private float trailTime = 0.25f;

    [Header("Facing")]
    [SerializeField] private bool invertFacing = true;

    [Header("Headbutt Aim")]
    [SerializeField] private bool  aimHeadAtPlayer = true;
    [Tooltip("Calibrate to the sprite's head-forward direction (degrees). Tune live in editor.")]
    [SerializeField] private float headAngleOffset = 0f;
    [Tooltip("Degrees per second the head eases back to upright after the dash.")]
    [SerializeField] private float aimResetSpeed = 720f;

    private Vector2 _spawnCenter;
    private Vector2 _waypoint;
    private bool _isAttacking;
    private bool _hasDamaged;
    private float _lastAttackTime = -999f;
    private Vector2 _preAttackPos;
    private Coroutine _attackCoroutine;
    private Animator _anim;
    private TrailRenderer _dashTrail;
    private ParticleSystem _dashParticles;

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
        SetupDashTrail();
        SetupDashParticles();
    }

    private void SetupDashTrail()
    {
        _dashTrail = gameObject.AddComponent<TrailRenderer>();
        _dashTrail.time = trailTime;
        _dashTrail.startWidth = trailStartWidth;
        _dashTrail.endWidth = trailEndWidth;
        _dashTrail.material = new Material(Shader.Find("Sprites/Default"));
        _dashTrail.numCornerVertices = 4;
        _dashTrail.numCapVertices = 4;
        _dashTrail.sortingOrder = GetComponent<SpriteRenderer>() != null
            ? GetComponent<SpriteRenderer>().sortingOrder - 1 : 0;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(trailStartColor, 0f),
                new GradientColorKey(trailEndColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(trailStartColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        _dashTrail.colorGradient = gradient;
        _dashTrail.emitting = false;
    }

    private void SetupDashParticles()
    {
        GameObject particleGO = new GameObject("HeadbuttParticles");
        particleGO.transform.SetParent(transform, false);
        particleGO.transform.localPosition = Vector3.zero;

        _dashParticles = particleGO.AddComponent<ParticleSystem>();
        var main = _dashParticles.main;
        main.loop = true;
        main.startLifetime = 0.2f;
        main.startSpeed = 0f;
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 0.85f, 0.5f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        var emission = _dashParticles.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 12f;

        var shape = _dashParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        var sizeOverLifetime = _dashParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        ));

        var colorOverLifetime = _dashParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient particleGradient = new Gradient();
        particleGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.15f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = particleGradient;

        var particleRenderer = particleGO.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = new Material(Shader.Find("Sprites/Default"));
        particleRenderer.sortingOrder = GetComponent<SpriteRenderer>() != null
            ? GetComponent<SpriteRenderer>().sortingOrder - 1 : 0;

        _dashParticles.Stop();
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
        SoundManager.PlaySound(SoundType.KIKI_ATTACK_WINDUP);

        // Pre-roll delay (preserves tuned wind-up feel)
        yield return new WaitForSeconds(headbuttLaunchDelay);

        // Gate: wait until the animator has actually entered Float_Attack.
        // The old WaitUntil had a !IsName bail-out that fired immediately when Play() was
        // still deferred and the state was Float_Idle — this is the root of the unreliability.
        yield return new WaitUntil(() =>
        {
            if (_anim == null) return true;
            return _anim.GetCurrentAnimatorStateInfo(0).IsName("Float_Attack");
        });

        // Now safely wait for the freeze frame — no bail-out needed, state is confirmed.
        float freezeNormalized = (float)attackFreezeFrame / attackTotalFrames;
        yield return new WaitUntil(() =>
        {
            if (_anim == null) return true;
            return _anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= freezeNormalized;
        });

        // Freeze the animator on that frame
        if (_anim != null) _anim.speed = 0f;

        // Lock in player position and launch direction
        Vector2 target = playerTransform != null ? (Vector2)playerTransform.position : _preAttackPos;
        Vector2 dashDir = target - (Vector2)transform.position;
        if (dashDir.sqrMagnitude < 0.001f) dashDir = Vector2.right;
        dashDir = dashDir.normalized;
        FlipTo(dashDir.x > 0);
        AimHeadAlong(dashDir);

        // Enable dash effects
        if (_dashTrail != null) { _dashTrail.Clear(); _dashTrail.emitting = true; }
        if (_dashParticles != null) _dashParticles.Play();
        SoundManager.PlaySound(SoundType.KIKI_ATTACK_DASH);

        // Dash — animator stays frozen on freeze frame
        float elapsed = 0f;
        while (elapsed < headbuttDuration)
        {
            elapsed += Time.deltaTime;
            if (rb != null) rb.linearVelocity = dashDir * headbuttSpeed;
            CheckHeadbuttHit();
            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Disable dash effects
        if (_dashTrail != null) _dashTrail.emitting = false;
        if (_dashParticles != null) _dashParticles.Stop();

        // Unfreeze and let the rest of the attack animation play
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.identity, aimResetSpeed * Time.deltaTime);
            yield return null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = _preAttackPos;
        }
        transform.rotation = Quaternion.identity;

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
                SoundManager.PlaySound(SoundType.KIKI_ATTACK_HIT);
                if (_anim != null) _anim.speed = 1f;
                return;
            }
        }
    }

    protected override void CleanupAttackVfx()
    {
        if (_dashTrail != null) { _dashTrail.emitting = false; _dashTrail.Clear(); }
        if (_dashParticles != null) _dashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    protected override void OnStunInterrupt()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        _isAttacking = false;
        transform.rotation = Quaternion.identity;
        if (_anim != null) _anim.speed = 1f;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (_dashTrail != null) { _dashTrail.emitting = false; _dashTrail.Clear(); }
        if (_dashParticles != null) _dashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    private void AimHeadAlong(Vector2 dir)
    {
        if (!aimHeadAtPlayer) return;
        // Only tilt vertically — horizontal facing is handled by FlipTo/localScale.x.
        // Negative localScale.x mirror-inverts Z-rotation, so negate the whole angle (tilt + offset) together.
        float tilt = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        float sign = transform.localScale.x < 0f ? -1f : 1f;
        transform.rotation = Quaternion.Euler(0f, 0f, sign * (tilt + headAngleOffset));
    }
}
