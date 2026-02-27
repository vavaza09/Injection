using UnityEngine;
using Game.Characters.Player;
using Core.Logging;
using VContainer;
using Game.Components.Movement;

public class Player : character
{
    private Core.Logging.ILogger _logger;

    [Header("Player Components")]
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animationController;
    private PlayerAudioController _audioController;
    [SerializeField] private DashComponent _dashComponent;

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
        _inputHandler?.UpdateAimDirection(characterTransform);

        if (_dashComponent.IsDashing)
        {
            SlowMotion.Instance.StopSlowMotion();
        }
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        Vector2 moveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;
        Move(moveInput);
    }

    public override void Move(Vector2 direction)
    {
        if (!isAlive) return;

        movementComponent?.Move(direction);
        movementComponent?.UpdateFacing(direction);
    }

    private void Dash()
    {   
        _logger?.Log("Dash initiated");
        Vector2 aimDir = _inputHandler != null ? _inputHandler.AimDirection : Vector2.zero;
        movementComponent?.Dash(aimDir);
        _audioController?.PlayDashSound();
    }

    public override void Attack()
    {
        if (!isAlive) return;

        _animationController?.PlayAttackAnimation();
        _audioController?.PlayAttackSound();

        _logger?.Log("Player attacked");
    }

    public void Jump()
    {
        if (!isAlive) return;

        if (movementComponent == null)
        {
            _logger?.LogError("Cannot jump: MovementComponent is null");
            return;
        }

        movementComponent.TryJump(particles: true, playSfx: true);
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

    protected override void OnTakeDamage()
    {
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

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_inputHandler != null)
        {
            _inputHandler.OnJumpPressed -= Jump;
            _inputHandler.OnJumpReleased -= CancelJump;
            _inputHandler.OnAttackPressed -= Attack;
            _inputHandler.OnRightClickPressed -= ActivateSlowMotion; 
            _inputHandler.Dispose();
        }
    }
}