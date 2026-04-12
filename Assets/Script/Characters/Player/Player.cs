using UnityEngine;
using Game.Characters.Player;
using Core.Logging;
using VContainer;
using Game.Components.Movement;
using System.Collections.Generic;

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

    [Header("References for DI")]
    [SerializeField] private Animator animator;

    [Header("Health Settings")]
    [SerializeField] private float invincibilityDuration = 2f;

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
            _inputHandler.OnRightClickPressed += ActivateSlowMotion;
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

        _inputHandler?.UpdateAimDirection(transform);
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
        _logger?.Log("Dash initiated");
        Vector2 aimDir = _inputHandler != null ? _inputHandler.AimDirection : Vector2.zero;

        if (aimDir == Vector2.zero)
        {
            float facing = characterTransform != null ? Mathf.Sign(characterTransform.localScale.x) : 1f;
            aimDir = new Vector2(facing, 0f);
        }

        movementComponent?.Dash(aimDir);
        _audioController?.PlayDashSound();
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

        int targetId = targetCharacter.GetInstanceID();
        if (_dashHitTargets.Contains(targetId))
        {
            return;
        }

        float damageMultiplier = movementComponent.MovementAttackMultiplier;
        float finalDamage = dashImpactBaseDamage * damageMultiplier;

        targetCharacter.TakeDamage(finalDamage);
        _dashHitTargets.Add(targetId);
        _lastDashHitTime = Time.time;

        _logger?.Log($"Dash impact hit {targetCharacter.name} for {finalDamage:F1} damage (x{damageMultiplier:F2})");
    }

    #endregion

    #region Health And Death

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
            _inputHandler.OnRightClickPressed -= ActivateSlowMotion; 
            _inputHandler.OnDashPressed -= Dash;
            _inputHandler.Dispose();
        }

        _dashHitTargets.Clear();
    }

    #endregion
}