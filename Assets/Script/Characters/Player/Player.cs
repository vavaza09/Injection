using UnityEngine;
using Game.Characters.Player;
using Core.Logging;
using VContainer;
using Game.Components.Movement;
using Game.UI.Movement;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class Player : character
{
    #region Fields

    private Core.Logging.ILogger _logger;

    [Header("Player Components")]
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animationController;
    private PlayerAudioController _audioController;

    [Header("Player Movement")]
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private int jumpsRemaining;

    [Header("Dependency")]
    [SerializeField] private Camera mainCamera;

    [Header("Slow Motion Settings")]
    [SerializeField] private float slowMotionTimeScale = 0.3f; 
    [SerializeField] private float slowMotionDuration = 2f;     
    [SerializeField] private bool useSmoothSlowMotion = true;   

    [Header("Dash Aim")]
    [SerializeField] private bool requireAimHoldForDash = true;
    [SerializeField] private DashAimDirectionDisplay dashAimDisplay;

    [Header("References for DI")]
    [SerializeField] private Animator animator;

    [Header("Health Settings")]
    [SerializeField] private float invincibilityDuration = 2f;

    [Header("Health Cheat (Debug)")]
    [SerializeField] private bool enableHealthCheats = true;
    [SerializeField] private Key cheatTakeHitKey = Key.F1;
    [SerializeField] private Key cheatHealHitKey = Key.F2;
    [SerializeField] private Key cheatFullHealKey = Key.F3;
    [SerializeField] private Key cheatInvincibleKey = Key.F4;
    [SerializeField] private Key cheatReviveKey = Key.F5;
    [SerializeField] private float cheatInvincibilityDuration = 5f;

    [Header("Movement Combat")]
    [SerializeField] private float dashImpactBaseDamage = 15f;
    [SerializeField] private float dashImpactCooldown = 0.15f;
    [SerializeField] private LayerMask dashDamageLayer = ~0;

    private readonly HashSet<int> _dashHitTargets = new HashSet<int>();
    private bool _wasDashing;
    private float _lastDashHitTime;

    #endregion

    #region Dependency Injection

    [Inject]
    public void Construct(
        LoggerFactory loggerFactory,
        PlayerInputHandler inputHandler,
        PlayerAnimationController animationController,
        PlayerAudioController audioController)
    {
        _logger = loggerFactory?.CreateLogger<Player>();
        _inputHandler = inputHandler;
        _animationController = animationController;
        _audioController = audioController;
        _logger?.Log("Player components injected via DI");
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            _logger?.LogWarning("Animator not assigned, finding in children...");
        }
    }

    protected override void Start()
    {
        base.Start();

        if (_inputHandler != null)
        {
            _inputHandler.SetCamera(mainCamera);
        }

        if (_animationController != null && movementComponent != null)
        {
            _animationController.SetMovementComponent(movementComponent);
        }

        if (_inputHandler != null)
        {
            _inputHandler.OnJumpPressed += Jump;
            _inputHandler.OnJumpReleased += CancelJump;
            _inputHandler.OnAttackPressed += Attack;
            _inputHandler.OnRightClickPressed += BeginDashAimMode;
            _inputHandler.OnRightClickReleased += EndDashAimMode;
            _inputHandler.OnDashPressed += Dash;

            _inputHandler.Enable();
        }
        else
        {
            _logger?.LogError("PlayerInputHandler not injected!");
        }

        _logger?.Log("Player initialized");
    }

    protected override void Update()
    {
        base.Update();

        _inputHandler?.UpdateAimDirection(transform);
        UpdateDashAimUI();

        HandleHealthCheatInput();

        if (!isAlive) return;

        _animationController?.UpdateMovementAnimation();

        if (movementComponent != null && movementComponent.IsGrounded())
        {
            jumpsRemaining = maxJumps;
        }

        if (movementComponent != null && movementComponent.IsDashing)
        {
            SlowMotion.Instance.StopSlowMotion();
        }

        // Reset dash-hit registry whenever a new dash starts.
        bool isDashingNow = movementComponent != null && movementComponent.IsDashing;
        if (isDashingNow && !_wasDashing)
        {
            _dashHitTargets.Clear();
        }
        _wasDashing = isDashingNow;
    }

    private void HandleHealthCheatInput()
    {
        if (!enableHealthCheats || healthComponent == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard[cheatTakeHitKey].wasPressedThisFrame)
        {
            int beforeHitCount = GetHitCount();
            TakeDamage(1f);
            int afterHitCount = GetHitCount();

            if (afterHitCount == beforeHitCount)
            {
                _logger?.Log("Cheat hit was blocked (invincible or already dead).");
            }
            else
            {
                _logger?.Log($"Cheat hit applied. Remaining {GetRemainingHits()}/{GetMaxHits()}, hits taken {GetHitCount()}");
            }
        }

        if (keyboard[cheatHealHitKey].wasPressedThisFrame)
        {
            healthComponent.Heal(1f);
            _logger?.Log($"Cheat heal +1 hit. Remaining {GetRemainingHits()}/{GetMaxHits()}, hits taken {GetHitCount()}");
        }

        if (keyboard[cheatFullHealKey].wasPressedThisFrame)
        {
            healthComponent.Heal(healthComponent.maxHealth);
            _logger?.Log($"Cheat full heal. Remaining {GetRemainingHits()}/{GetMaxHits()}, hits taken {GetHitCount()}");
        }

        if (keyboard[cheatInvincibleKey].wasPressedThisFrame)
        {
            healthComponent.StartInvincibility(cheatInvincibilityDuration);
            _logger?.Log($"Cheat invincibility started for {cheatInvincibilityDuration:F1}s");
        }

        if (keyboard[cheatReviveKey].wasPressedThisFrame)
        {
            healthComponent.Heal(healthComponent.maxHealth);
            if (!isAlive)
            {
                isAlive = true;
                movementComponent?.SetCanMove(true);

                Collider2D col = GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = true;
                }

                _logger?.LogWarning("Cheat revive applied.");
            }

            _logger?.Log($"Cheat revive/full reset. Remaining {GetRemainingHits()}/{GetMaxHits()}, hits taken {GetHitCount()}");
        }
    }

    #endregion

    #region Physics Update

    private void FixedUpdate()
    {
        if (!isAlive) return;

        Vector2 moveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;
        Move(moveInput);
    }

    #endregion

    #region Movement And Actions

    public override void Move(Vector2 direction)
    {
        if (!isAlive) return;

        movementComponent?.Move(direction);

        if (direction.x != 0)
        {
            characterTransform.localScale = new Vector3(
                Mathf.Sign(direction.x),
                1,
                1
            );
        }
    }

    private void Dash()
    {   
        if (!isAlive) return;
        if (movementComponent == null) return;

        if (requireAimHoldForDash && (_inputHandler == null || !_inputHandler.IsAimHeld))
        {
            _logger?.Log("Dash blocked: hold right click to enter aim slow-motion mode first.");
            return;
        }

        _logger?.Log("Dash initiated");
        Vector2 aimDir = ResolveDashDirection();
        int dashesBefore = movementComponent.CurrentDashes;

        movementComponent.Dash(aimDir);

        if (movementComponent.IsDashing || movementComponent.CurrentDashes < dashesBefore)
        {
            _audioController?.PlayDashSound();
        }
    }

    public void Jump()
    {
        if (!isAlive) return;

        if (movementComponent == null)
        {
            _logger?.LogError("Cannot jump: MovementComponent is null");
            return;
        }

        movementComponent.Jump(particles: true, playSfx: true);
    }

    public void CancelJump()
    {
        if (!isAlive) return;
        movementComponent?.CancelJump();
    }

    private void ActivateSlowMotion()
    {
        if (!isAlive) return;

        if (useSmoothSlowMotion)
        {
            SlowMotion.Instance.StartSlowMotionSmooth(
                slowMotionTimeScale,
                slowMotionDuration,
                easeInDuration: 0.1f,
                easeOutDuration: 0.3f
            );
        }
        else
        {
            SlowMotion.Instance.StartSlowMotion(
                slowMotionTimeScale,
                slowMotionDuration
            );
        }

        _logger?.Log($"Slow Motion activated! TimeScale: {slowMotionTimeScale}, Duration: {slowMotionDuration}s");
    }

    private void BeginDashAimMode()
    {
        if (!isAlive) return;
        ActivateSlowMotion();
    }

    private void EndDashAimMode()
    {
        SlowMotion.Instance.StopSlowMotion();
    }

    private void UpdateDashAimUI()
    {
        if (dashAimDisplay == null)
        {
            return;
        }

        bool isAimHeld = isAlive && _inputHandler != null && _inputHandler.IsAimHeld;
        bool canDashNow = movementComponent != null && movementComponent.CanDash;
        bool isDashingNow = movementComponent != null && movementComponent.IsDashing;

        dashAimDisplay.Refresh(ResolveDashDirection(), isAimHeld, canDashNow, isDashingNow);
    }

    private Vector2 ResolveDashDirection()
    {
        Vector2 aimDir = _inputHandler != null ? _inputHandler.AimDirection : Vector2.zero;

        if (aimDir.sqrMagnitude <= 0.0001f)
        {
            float facing = characterTransform != null ? Mathf.Sign(characterTransform.localScale.x) : 1f;
            if (Mathf.Approximately(facing, 0f))
            {
                facing = 1f;
            }

            aimDir = new Vector2(facing, 0f);
        }

        return aimDir.normalized;
    }

    #endregion

    #region Combat

    public override void Attack()
    {
        if (!isAlive) return;

        _animationController?.PlayAttackAnimation();
        _audioController?.PlayAttackSound();

        _logger?.Log("Player attacked");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealDashImpactDamage(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDashImpactDamage(other);
    }

    private void TryDealDashImpactDamage(Collider2D targetCollider)
    {
        if (!isAlive || movementComponent == null || targetCollider == null)
        {
            return;
        }

        if (!movementComponent.DashAttacking)
        {
            return;
        }

        if ((dashDamageLayer.value & (1 << targetCollider.gameObject.layer)) == 0)
        {
            return;
        }

        if (Time.time - _lastDashHitTime < dashImpactCooldown)
        {
            return;
        }

        character targetCharacter = targetCollider.GetComponentInParent<character>();
        if (targetCharacter == null || targetCharacter == this)
        {
            return;
        }

        if (targetCharacter is Enemy)
        {
            EnemyWeakPoint weakPoint = targetCollider.GetComponent<EnemyWeakPoint>();
            if (weakPoint == null)
            {
                EnemyWeakPoint[] weakPointConfigs = targetCharacter.GetComponentsInChildren<EnemyWeakPoint>();
                for (int i = 0; i < weakPointConfigs.Length; i++)
                {
                    EnemyWeakPoint weakPointConfig = weakPointConfigs[i];
                    if (weakPointConfig != null && weakPointConfig.IsWeakPoint(targetCollider))
                    {
                        weakPoint = weakPointConfig;
                        break;
                    }
                }
            }

            if (weakPoint == null)
            {
                return;
            }

            if (weakPoint.OwnerEnemy != null && weakPoint.OwnerEnemy != targetCharacter)
            {
                return;
            }
        }

        int targetId = targetCharacter.GetInstanceID();
        if (_dashHitTargets.Contains(targetId))
        {
            return;
        }

        float impactMultiplier = movementComponent.MovementAttackMultiplier;
        float impactPower = dashImpactBaseDamage * impactMultiplier;

        targetCharacter.TakeDamage(1f);
        _dashHitTargets.Add(targetId);
        _lastDashHitTime = Time.time;

        _logger?.Log($"Dash impact hit {targetCharacter.name}, applied 1 hit (impact power {impactPower:F1}, x{impactMultiplier:F2})");
    }

    #endregion

    #region Health And Death

    public int GetRemainingHits()
    {
        return healthComponent != null ? healthComponent.GetRemainingHitCount() : 0;
    }

    public int GetHitCount()
    {
        return healthComponent != null ? healthComponent.GetHitCount() : 0;
    }

    public int GetMaxHits()
    {
        return healthComponent != null ? Mathf.CeilToInt(healthComponent.maxHealth) : Mathf.CeilToInt(maxHealth);
    }

    protected override void OnTakeDamage()
    {
        movementComponent?.NotifyDamageTaken();
        healthComponent?.StartInvincibility(invincibilityDuration);
        _audioController?.PlayHurtSound();
        OnInvincibilityVisual();
    }

    private void OnInvincibilityVisual()
    {
    }

    protected override void OnDeath()
    {
        movementComponent?.SetCanMove(false);
        _animationController?.PlayDeathAnimation();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        _logger?.LogWarning("Player died! Game Over!");
    }

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_inputHandler != null)
        {
            _inputHandler.OnJumpPressed -= Jump;
            _inputHandler.OnJumpReleased -= CancelJump;
            _inputHandler.OnAttackPressed -= Attack;
            _inputHandler.OnRightClickPressed -= BeginDashAimMode;
            _inputHandler.OnRightClickReleased -= EndDashAimMode;
            _inputHandler.OnDashPressed -= Dash;
            _inputHandler.Dispose();
        }

        if (FindAnyObjectByType<SlowMotion>() != null)
        {
            SlowMotion.Instance.StopSlowMotion();
        }

        _dashHitTargets.Clear();
    }

    #endregion
}