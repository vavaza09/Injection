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
    [SerializeField] private float telegraphDuration = 0.8f;
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

    [Header("Shadow")]
    [SerializeField] private GameObject shadowObject;
    [SerializeField] private Vector3 minShadowScale = new Vector3(2.8f, 0.5f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource _sfxSource;

    private enum StompPhase { None, Jump, Fall, Land }
    private StompPhase _stompPhase = StompPhase.None;

    private bool isAttacking;
    private bool hasGroundImpact;
    private bool _airContactHasHit;
    private float lastAttackTime = -999f;
    private Coroutine _stompRoutineHandle;
    private Coroutine _damageWindowHandle;
    private Vector3 _shadowBaseScale;
    private float _shadowGroundY;
    private float _shadowZ;
    private float _targetLandingY;
    private bool _useTargetLandingY;
    private const float FeetOffset = 0.9f;

    private Collider2D stomperBodyCollider;
    private Collider2D[] _solidColliders;
    private CinemachineImpulseSource _impulseSource;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        if (_sfxSource == null) _sfxSource = GetComponent<AudioSource>();

        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground", "Platform");

        if (stompDamageZone == null)
            stompDamageZone = GetComponentInChildren<StompDamageZone>();

        stomperBodyCollider = GetComponent<Collider2D>();

        // All solid (non-trigger) colliders — root body + child weak-point colliders.
        // These are toggled off during flight so the stomper passes through platforms.
        Collider2D[] all = GetComponentsInChildren<Collider2D>(true);
        var solid = new System.Collections.Generic.List<Collider2D>();
        foreach (Collider2D c in all)
            if (c != null && !c.isTrigger) solid.Add(c);
        _solidColliders = solid.ToArray();

        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null)
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
    }

    protected override void Start()
    {
        base.Start();

        // Prefer an existing scene child named "Stomper_shadow" (correctly placed in the scene).
        // Fall back to the Inspector reference only if no child is found.
        Transform shadowChild = transform.Find("Stomper_shadow");
        if (shadowChild != null)
            shadowObject = shadowChild.gameObject;
        else if (shadowObject != null && !shadowObject.scene.IsValid())
            shadowObject = Instantiate(shadowObject, transform.position + new Vector3(0, -2.45f, 0), Quaternion.identity, transform);

        if (shadowObject != null)
        {
            _shadowBaseScale = shadowObject.transform.localScale;
            _shadowGroundY = shadowObject.transform.position.y;
            _shadowZ = shadowObject.transform.position.z;
            shadowObject.SetActive(false);
        }
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
        if (shadowObject != null) shadowObject.SetActive(false);
        SetSolidCollidersEnabled(true);

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
        _useTargetLandingY = false;
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

        float apexY = apexAboveStart.y;
        if (shadowObject != null)
            shadowObject.SetActive(true);

        _stompPhase = StompPhase.Jump;
        SoundManager.PlaySoundOn(SoundType.STOMPER_JUMP, _sfxSource);
        SetSolidCollidersEnabled(false);

        // Phase 1: rise straight up at jumpUpSpeed (units/sec)
        float riseDistance = Mathf.Abs(apexAboveStart.y - startPosition.y);
        float riseDuration = jumpUpSpeed > 0f ? riseDistance / jumpUpSpeed : 0f;
        float timer = 0f;
        while (timer < riseDuration)
        {
            timer += Time.deltaTime;
            float t = riseDuration > 0f ? Mathf.Clamp01(timer / riseDuration) : 1f;
            transform.position = Vector3.Lerp(startPosition, apexAboveStart, t);
            // Shadow tracks the player while the stomper is still airborne
            UpdateShadow(transform.position.y, apexY, playerTransform.position.x);
            yield return null;
        }


        // Phase 2: slide horizontally over the player over jumpToPlayerTime
        timer = 0f;
        while (timer < jumpToPlayerTime)
        {
            timer += Time.deltaTime;
            float t = jumpToPlayerTime > 0f ? Mathf.Clamp01(timer / jumpToPlayerTime) : 1f;
            float targetX = playerTransform != null ? playerTransform.position.x : targetAbovePlayer.x;
            Vector3 slideTarget = new Vector3(targetX, apexAboveStart.y, startPosition.z);
            transform.position = Vector3.Lerp(apexAboveStart, slideTarget, t);
            UpdateShadow(transform.position.y, apexY, playerTransform.position.x);
            yield return null;
        }

        // Telegraph: lock stomper X and shadow at the player's current position.
        // Hold for telegraphDuration so the player can see where the stomp lands and dodge.
        float lockedShadowX = playerTransform != null ? playerTransform.position.x : transform.position.x;
        transform.position = new Vector3(lockedShadowX, transform.position.y, transform.position.z);

        // Find the ground directly beneath the shadow column (locked X) by raycasting
        // downward from the apex. This is the single source of truth for both the shadow
        // position and the landing snap — the Stomper will land exactly where the shadow is.
        _useTargetLandingY = TryGetGroundYBelow(lockedShadowX, transform.position.y, out _targetLandingY);
        if (!_useTargetLandingY) _targetLandingY = _shadowGroundY;

        float telegraphTimer = 0f;
        while (telegraphTimer < telegraphDuration)
        {
            telegraphTimer += Time.deltaTime;
            UpdateShadow(transform.position.y, apexY, lockedShadowX);
            yield return null;
        }

        _stompPhase = StompPhase.Fall;
        SoundManager.PlaySoundOn(SoundType.STOMPER_FALL_RUSH, _sfxSource);
        float savedGravityScale = 0f;
        if (rb != null)
        {
            savedGravityScale = rb.gravityScale;
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, -Mathf.Abs(stompDownSpeed));
        }

        while (!hasGroundImpact)
        {
            if (rb != null)
                rb.linearVelocity = new Vector2(0f, -Mathf.Abs(stompDownSpeed));
            UpdateShadow(transform.position.y, apexY, lockedShadowX);
            if (_useTargetLandingY)
            {
                // Land on the player's ground only — ignore platforms above it.
                if (transform.position.y - FeetOffset <= _targetLandingY)
                {
                    // Snap so a fast fall doesn't overshoot/clip into the floor.
                    transform.position = new Vector3(
                        transform.position.x, _targetLandingY + FeetOffset, transform.position.z);
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    hasGroundImpact = true;
                    break;
                }
            }
            else if (HasLanded())
            {
                hasGroundImpact = true;
                break;
            }
            yield return null;
        }

        // STOMP_LAND: halt the fall, slam the ground.
        _stompPhase = StompPhase.Land;
        if (shadowObject != null) shadowObject.SetActive(false);
        SetSolidCollidersEnabled(true);
        if (rb != null)
        {
            rb.gravityScale = savedGravityScale;
            rb.linearVelocity = Vector2.zero;
        }

        SpawnImpactVFX();
        SoundManager.PlaySoundOn(SoundType.STOMPER_STOMP_IMPACT, _sfxSource);

        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(shakeAmplitude);

        _damageWindowHandle = StartCoroutine(StompDamageWindow());
        yield return _damageWindowHandle;

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

    private void SetSolidCollidersEnabled(bool enabled)
    {
        if (_solidColliders == null) return;
        foreach (Collider2D c in _solidColliders)
            if (c != null) c.enabled = enabled;
    }

    private bool HasLanded()
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(0f, -FeetOffset);
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

    private bool TryGetGroundYBelow(float worldX, float fromY, out float groundY)
    {
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(worldX, fromY), Vector2.down, 100f, groundLayer);
        groundY = hit.collider != null ? hit.point.y : 0f;
        return hit.collider != null;
    }


    // Pins the shadow to a fixed ground Y/Z at the given world X, and scales it by
    // the stomper's height: full base scale at apex, min scale near the ground.
    // World position is set every frame so the shadow stays put even though it is
    // parented to the (moving) stomper.
    private void UpdateShadow(float stomperY, float apexY, float worldX)
    {
        if (shadowObject == null) return;
        // Once the landing target is locked at telegraph, pin shadow to it.
        // Before that, project the shadow down to the actual ground beneath its column
        // so it sits on whichever platform is directly below — not the Stomper's spawn ground.
        float groundY;
        if (_useTargetLandingY)
            groundY = _targetLandingY;
        else if (!TryGetGroundYBelow(worldX, stomperY, out groundY))
            groundY = _shadowGroundY;
        shadowObject.transform.position = new Vector3(worldX, groundY, _shadowZ);
        float maxH = apexY - groundY;
        float t = maxH > 0f ? Mathf.Clamp01((stomperY - groundY) / maxH) : 0f;
        shadowObject.transform.localScale = Vector3.Lerp(minShadowScale, _shadowBaseScale, t);
    }

    private void OnDrawGizmosSelected()
    {
        if (_stompPhase != StompPhase.Fall) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 1.1f);
    }
}
