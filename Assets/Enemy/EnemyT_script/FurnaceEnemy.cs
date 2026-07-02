using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FurnaceEnemy : Enemy
{
    [Header("Furnace Ranged Attack")]
    public float attackCooldown = 1.25f;

    [Header("Double Shot")]
    [SerializeField] private MuzzleFlashEffect muzzleFlash;

    [Header("Muzzle Flash Timing")]
    [Tooltip("Seconds into the attack windup before the muzzle flash glow appears")]
    [SerializeField] private float flashStartDelay = 0.5f;

    [Tooltip("Seconds into the attack windup before smoke appears")]
    [SerializeField] private float firstShotDelay = 0.8f;

    [Tooltip("Seconds between first and second projectile")]
    [SerializeField] private float secondShotDelay = 0.15f;

    [Tooltip("Seconds after last shot before attack ends")]
    [SerializeField] private float postAttackDelay = 0.2f;

    [Header("Mortar Attack")]
    [SerializeField] private float launchUpSpeed = 20f;
    [SerializeField] private float hangTime = 2f;
    [SerializeField] private float dropSpawnHeight = 12f;
    [SerializeField] private float dropFallSpeed = 25f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float explosionDamage = 1.5f;

    [Header("Explosion VFX")]
    [SerializeField] private float vfxFlashMultiplier = 2.5f;
    [SerializeField] private int vfxFireballCount = 30;
    [SerializeField] private int vfxSmokeCount = 13;
    [SerializeField] private int vfxSparkCount = 27;
    [SerializeField] private float vfxRingMultiplier = 3f;

    [Header("Facing")]
    [Tooltip("Enable if the sprite sheet faces LEFT by default (positive scale = left-facing).")]
    [SerializeField] private bool invertFacing = true;

    public override bool SpriteFacesLeft => invertFacing;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce  = 8f;
    [SerializeField] private float knockbackUpward = 0.3f;

    [Header("Audio")]
    [SerializeField] private AudioSource _sfxSource;

    private float lastAttackTime = -999f;
    private bool _isAttacking;
    private Coroutine _attackRoutine;
    private readonly List<GameObject> _activeMortarVfx = new List<GameObject>();

    protected override void Awake()
    {
        base.Awake();
        if (_sfxSource == null) _sfxSource = GetComponent<AudioSource>();
        Make3D(_sfxSource);
    }
    protected override void Start() => base.Start();
    protected override void Update() => base.Update();

    public override void DetectPlayer()
    {
        if (_isAttacking) return;
        base.DetectPlayer();
    }

    public override void Patrol(Vector2 direction, float speed)
    {
        if (_isAttacking) return;
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
        if (_isAttacking) return;
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
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Attack()
    {
        if (playerTransform == null || _isAttacking) return;
        if (Time.time < lastAttackTime + attackCooldown) return;
        _attackRoutine = StartCoroutine(AttackRoutine());
    }

    protected override void OnStunInterrupt()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        _isAttacking = false;
        if (_animator != null)
            _animator.SetBool("IsAttacking", false);
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        lastAttackTime = Time.time;

        if (_animator != null)
            _animator.SetBool("IsAttacking", true);

        // Muzzle flash timing
        if (flashStartDelay > 0f)
            yield return new WaitForSeconds(flashStartDelay);
        SoundManager.PlaySoundOn(SoundType.FURNACE_ATTACK_CHARGE, _sfxSource);

        // === FIRST SHOT ===
        if (muzzleFlash != null) muzzleFlash.PlayFlash(Vector2.up);
        float remaining = Mathf.Max(0f, firstShotDelay - flashStartDelay);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
        if (muzzleFlash != null) muzzleFlash.PlaySmoke(Vector2.up);

        Vector2 savedPos1 = playerTransform.position;
        SpawnLaunchBullet();
        StartCoroutine(MortarDropSequence(savedPos1));

        // === SECOND SHOT after delay ===
        yield return new WaitForSeconds(secondShotDelay);

        if (muzzleFlash != null) muzzleFlash.PlayFlash(Vector2.up);
        yield return new WaitForSeconds(0.1f);
        if (muzzleFlash != null) muzzleFlash.PlaySmoke(Vector2.up);

        Vector2 savedPos2 = playerTransform != null ? (Vector2)playerTransform.position : savedPos1;
        SpawnLaunchBullet();
        StartCoroutine(MortarDropSequence(savedPos2));

        // Wait for both drops to finish (hangTime + buffer for fall + explosion)
        yield return new WaitForSeconds(hangTime + 1.5f);

        if (postAttackDelay > 0f)
            yield return new WaitForSeconds(postAttackDelay);

        if (_animator != null)
            _animator.SetBool("IsAttacking", false);

        _isAttacking = false;
        SetState(EnemyState.Idle);
    }

    private IEnumerator MortarDropSequence(Vector2 savedPos)
    {
        LayerMask groundMask = LayerMask.GetMask("Ground");
        float groundY = savedPos.y;
        RaycastHit2D groundHit = Physics2D.Raycast(
            new Vector2(savedPos.x, savedPos.y + 1f), Vector2.down, 30f, groundMask);
        if (groundHit.collider != null)
            groundY = groundHit.point.y;

        // Warning appears after 40% of hang time
        float earlyWait = hangTime * 0.4f;
        yield return new WaitForSeconds(earlyWait);

        GameObject warning = new GameObject("MortarWarning");
        warning.transform.position = new Vector3(savedPos.x, groundY + 0.05f, 0f);
        var warnSR = warning.AddComponent<SpriteRenderer>();
        warnSR.sprite = CircleSprite(64);
        warnSR.color = new Color(1f, 0.1f, 0.1f, 0.4f);
        warnSR.sortingOrder = 4;
        warning.transform.localScale = Vector3.one * explosionRadius * 2f;
        warning.AddComponent<WarningPulse>();
        _activeMortarVfx.Add(warning);

        yield return new WaitForSeconds(hangTime - earlyWait);

        // Spawn drop bullet above ground by dropSpawnHeight
        GameObject dropObj = new GameObject("FurnaceDropBullet");
        dropObj.transform.position = new Vector3(savedPos.x, groundY + dropSpawnHeight, 0f);
        var dropSR = dropObj.AddComponent<SpriteRenderer>();
        dropSR.sprite = CircleSprite(24);
        dropSR.color = new Color(1f, 0.3f, 0f);
        dropSR.sortingOrder = 5;
        dropObj.transform.localScale = Vector3.one * 0.5f;

        var dropTrail = dropObj.AddComponent<TrailRenderer>();
        dropTrail.time = 0.25f;
        dropTrail.startWidth = 0.3f;
        dropTrail.endWidth = 0.05f;
        dropTrail.material = new Material(Shader.Find("Sprites/Default"));
        Gradient tg = new Gradient();
        tg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        dropTrail.colorGradient = tg;
        _activeMortarVfx.Add(dropObj);

        // Fall until ground
        SoundManager.PlaySoundOn(SoundType.FURNACE_MORTAR_DESCENDING, _sfxSource);
        while (dropObj != null && dropObj.transform.position.y > groundY)
        {
            dropObj.transform.position += Vector3.down * dropFallSpeed * Time.deltaTime;
            yield return null;
        }

        if (dropObj != null) { Destroy(dropObj); _activeMortarVfx.Remove(dropObj); }
        if (warning != null) { Destroy(warning); _activeMortarVfx.Remove(warning); }

        Vector2 impactPos = new Vector2(savedPos.x, groundY);

        SoundManager.PlaySoundOn(SoundType.FURNACE_EXPLOSION, _sfxSource);
        GameObject explGO = new GameObject("MortarExplosion");
        explGO.transform.position = new Vector3(impactPos.x, impactPos.y, 0f);
        var explFX = explGO.AddComponent<MortarExplosionVFX>();
        explFX.flashSizeMultiplier = vfxFlashMultiplier;
        explFX.fireballCount = vfxFireballCount;
        explFX.smokeCount = vfxSmokeCount;
        explFX.sparkCount = vfxSparkCount;
        explFX.ringMultiplier = vfxRingMultiplier;
        explFX.Initialize(explosionRadius);

        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPos, explosionRadius);
        foreach (var col in hits)
        {
            Player p = col.GetComponentInParent<Player>();
            if (p != null)
            {
                if (p.TakeDamage(explosionDamage))
                    p.ApplyKnockback(impactPos, knockbackForce, knockbackUpward);
                break;
            }
        }
    }

    private void SpawnLaunchBullet()
    {
        SoundManager.PlaySoundOn(SoundType.FURNACE_MORTAR_LAUNCH, _sfxSource);
        GameObject launchObj = new GameObject("FurnaceLaunchBullet");
        launchObj.transform.position = transform.position + Vector3.up * 0.5f;

        var sr = launchObj.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSprite(32);
        sr.color = new Color(1f, 0.7f, 0.15f, 1f);
        sr.sortingOrder = 10;
        launchObj.transform.localScale = Vector3.one * 0.6f;

        var trail = launchObj.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.25f;
        trail.endWidth = 0.03f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.05f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = g;

        GameObject glow = new GameObject("LaunchGlow");
        glow.transform.SetParent(launchObj.transform, false);
        var glowSr = glow.AddComponent<SpriteRenderer>();
        glowSr.sprite = CircleSprite(32);
        glowSr.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        glowSr.sortingOrder = 9;
        glow.transform.localScale = Vector3.one * 2.5f;

        var launcher = launchObj.AddComponent<FurnaceLaunchBullet>();
        launcher.speed = launchUpSpeed;
    }

    protected override void CleanupAttackVfx()
    {
        foreach (GameObject go in _activeMortarVfx)
        {
            if (go != null) Destroy(go);
        }
        _activeMortarVfx.Clear();
    }

    private static Sprite CircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - dist / center);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
