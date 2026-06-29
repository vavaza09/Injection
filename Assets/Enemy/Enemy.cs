using Game.Components.Health;
using Game.Components.Movement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : character
{
    [Header("Enemy Settings")]
    [SerializeField] protected EnemyType enemyType;

    private Collider2D _bodyCollider;
    private Collider2D[] _allBodyColliders;
    private Collider2D _playerCollider;

    // Root-only solid collider — used by Stomp to toggle .enabled directly.
    protected Collider2D BodyCollider =>
        _bodyCollider != null ? _bodyCollider : (_bodyCollider = GetComponent<Collider2D>());

    // All solid (non-trigger) colliders on this enemy including children.
    // Cached once so IgnorePlayerCollision / WaitUntilClearOfPlayer handle
    // enemies that have extra child colliders (e.g. Kiki's "floating area").
    private Collider2D[] AllBodyColliders
    {
        get
        {
            if (_allBodyColliders == null)
            {
                // Exclude weak point colliders so they stay hittable during pass-through.
                var weakPointSet = new HashSet<Collider2D>();
                foreach (var wp in GetComponentsInChildren<EnemyWeakPoint>())
                {
                    foreach (var c in wp.WeakPointColliders)
                        if (c != null) weakPointSet.Add(c);
                    var ownCol = wp.GetComponent<Collider2D>();
                    if (ownCol != null) weakPointSet.Add(ownCol);
                }

                var all = GetComponentsInChildren<Collider2D>();
                int count = 0;
                foreach (var c in all) if (!c.isTrigger && !weakPointSet.Contains(c)) count++;
                _allBodyColliders = new Collider2D[count];
                int idx = 0;
                foreach (var c in all) if (!c.isTrigger && !weakPointSet.Contains(c)) _allBodyColliders[idx++] = c;
            }
            return _allBodyColliders;
        }
    }

    protected Collider2D PlayerCollider
    {
        get
        {
            if (_playerCollider == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _playerCollider = p.GetComponent<Collider2D>();
            }
            return _playerCollider;
        }
    }

    protected void IgnorePlayerCollision(bool ignore)
    {
        if (PlayerCollider == null) return;
        foreach (var col in AllBodyColliders)
            Physics2D.IgnoreCollision(col, PlayerCollider, ignore);
    }

    protected IEnumerator WaitUntilClearOfPlayer()
    {
        if (PlayerCollider == null) yield break;
        int timeout = 0;
        bool touching;
        do
        {
            touching = false;
            foreach (var col in AllBodyColliders)
            {
                if (Physics2D.IsTouching(col, PlayerCollider)) { touching = true; break; }
            }
            if (!touching) break;
            if (++timeout > 120) break;
            yield return null;
        } while (true);
        yield return null;
        yield return null;
    }

    [Header("Detection")]
    [SerializeField] protected float detectionRange = 6f;
    [SerializeField] protected float attackRange = 1.5f;

    [Header("Target")]
    [SerializeField] public Transform playerTransform; // Changed from protected to public

    [Header("State")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;

    [Header("Stun")]
    [SerializeField] private Color stunTintColor = new Color(0.3f, 0.9f, 1f, 1f);
    [SerializeField] private float stunBlinkInterval = 0.12f;
    [SerializeField] private GameObject stunVfxPrefab;

    [Header("Death")]
    [Tooltip("Small delay (seconds) before the death SFX plays.")]
    [SerializeField] private float deathSoundDelay = 0.1f;

    private float _stunEndTime;
    private bool _stunVisualsActive;
    private Coroutine _stunBlinkRoutine;
    private SpriteRenderer _spriteRenderer;
    protected Animator _animator;
    private Color _originalSpriteColor;
    private GameObject _activeStunVfx;

    public bool IsStunned => currentState == EnemyState.Stunned && isAlive;

    public virtual bool SpriteFacesLeft => false;


    protected override void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalSpriteColor = _spriteRenderer.color;

        _animator = GetComponentInChildren<Animator>();

        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: Player not found! Make sure player has 'Player' tag.");
            }
        }

        base.Start();
    }


    public override void Move(Vector2 direction)
    {
        // Prefer MovementComponent (recommended)
        if (movementComponent != null)
        {
            movementComponent.Move(direction);
        }
        else
        {
            transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Patrol movement with custom speed.
    /// </summary>
    public virtual void Patrol(Vector2 direction, float speed)
    {
        if (movementComponent != null)
        {
            // Use custom patrol speed
            Vector2 movement = direction.normalized * speed * Time.deltaTime;
            transform.position += (Vector3)movement;
        }
        else
        {
            transform.position += (Vector3)(direction.normalized * speed * Time.deltaTime);
        }
    }

    public override void Attack()
    {
        //if (attackComponent != null)
        //{
        //    attackComponent.PerformAttack();
        //}
    }

    protected override void Update()
    {
        base.Update();
        // Restore the normal visual state when the stun expires, OR when something
        // else (e.g. an attack coroutine finishing) pulled the enemy out of the
        // Stunned state without routing through EndStun. Without this second guard
        // the frozen animation / blink tint / electric VFX would linger while the
        // enemy resumes attacking the player.
        if (_stunVisualsActive && (!IsStunned || Time.time >= _stunEndTime))
            EndStun();
    }

    public virtual void Stun(float duration)
    {
        if (!isAlive) return;

        _stunEndTime = Time.time + duration;

        if (IsStunned) return;

        OnStunInterrupt();
        SetState(EnemyState.Stunned);
        _stunVisualsActive = true;

        if (movementComponent != null)
            movementComponent.Stop();

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (_animator != null)
            _animator.speed = 0f;

        if (_stunBlinkRoutine != null)
            StopCoroutine(_stunBlinkRoutine);
        _stunBlinkRoutine = StartCoroutine(StunBlinkRoutine());

        if (_activeStunVfx != null)
            Destroy(_activeStunVfx);

        if (stunVfxPrefab != null)
        {
            _activeStunVfx = Instantiate(stunVfxPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
            Bounds bounds = _spriteRenderer != null
                ? _spriteRenderer.bounds
                : new Bounds(transform.position, Vector3.one * 0.5f);
            _activeStunVfx = StunElectricFX.Attach(transform, bounds);
        }
    }

    protected virtual void OnStunInterrupt() { }

    protected virtual void CleanupAttackVfx() { }

    private void EndStun()
    {
        _stunVisualsActive = false;

        if (_animator != null)
            _animator.speed = 1f;

        if (_stunBlinkRoutine != null)
        {
            StopCoroutine(_stunBlinkRoutine);
            _stunBlinkRoutine = null;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalSpriteColor;

        if (_activeStunVfx != null)
        {
            Destroy(_activeStunVfx);
            _activeStunVfx = null;
        }

        if (!isAlive) return;

        SetState(EnemyState.Idle);
    }

    private IEnumerator StunBlinkRoutine()
    {
        if (_spriteRenderer == null) yield break;
        while (true)
        {
            _spriteRenderer.color = stunTintColor;
            yield return new WaitForSeconds(stunBlinkInterval);
            _spriteRenderer.color = _originalSpriteColor;
            yield return new WaitForSeconds(stunBlinkInterval);
        }
    }

    protected override void OnDeath()
    {
        // Play the death SFX globally (not on this enemy's own AudioSource) so it
        // isn't cut off when the GameObject is destroyed by the death/explosion FX.
        // Delayed via SoundManager's own coroutine so the wait survives this object's destruction.
        SoundManager.PlaySoundDelayed(SoundType.ENEMY_DEATH, deathSoundDelay);

        _stunVisualsActive = false;

        if (_stunBlinkRoutine != null)
        {
            StopCoroutine(_stunBlinkRoutine);
            _stunBlinkRoutine = null;
        }
        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalSpriteColor;
        if (_animator != null)
            _animator.speed = 1f;
        if (_activeStunVfx != null)
        {
            Destroy(_activeStunVfx);
            _activeStunVfx = null;
        }

        currentState = EnemyState.Dead;

        StopAllCoroutines();
        CleanupAttackVfx();

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        if (movementComponent != null)
            movementComponent.Stop();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
            col.enabled = false;

        EnemyDeathExplosion deathFX = GetComponent<EnemyDeathExplosion>();
        if (deathFX != null)
            deathFX.TriggerDeath();
        else
            Destroy(gameObject, 1.5f);
    }

    protected override void OnTakeDamage()
    {
        if (IsStunned) return;
        if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
        {
            currentState = EnemyState.Chase;
        }
    }


    public virtual void DetectPlayer()
    {
        if (IsStunned) return;

        // Early return if player not found
        if (playerTransform == null)
        {
            // Try to find player again (in case it spawned later)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                // Player still not found, stay in current state
                return;
            }
        }

        // Check if player transform is still valid (player might have been destroyed)
        if (playerTransform == null)
        {
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (dist <= detectionRange)
        {
            currentState = EnemyState.Chase;
        }
        else if (currentState != EnemyState.Idle && currentState != EnemyState.Patrol)
        {
            currentState = EnemyState.Idle;
        }
    }

    public virtual void ChasePlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector2 dir = playerTransform.position - transform.position;
        dir.y = 0f;

        Move(dir.normalized);
    }

    public virtual void PatrolArea()
    {
        // Stop movement (called when waiting at patrol point)
        Move(Vector2.zero);
    }

    public void SetState(EnemyState state)
    {
        currentState = state;
    }

    public EnemyState GetState()
    {
        return currentState;
    }
}
