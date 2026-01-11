using UnityEngine;
using Game.Characters.Player;
using Core.Logging;
using VContainer;

public class Player : character
{
    private Core.Logging.ILogger _logger;

    [Header("Player Components (Injected via DI)")]
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animationController;
    private PlayerAudioController _audioController;

    [Header("Player Movement")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxJumps = 2;
    private int jumpsRemaining;

    [Header("Player Stats")]
    [SerializeField] private int score = 0;

    [Header("References for DI")]
    [SerializeField] private Animator animator;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip hurtSound;

    // Constructor Injection via VContainer
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

        // Validate Animator
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            _logger?.LogWarning("Animator not assigned, finding in children...");
        }
    }

    protected override void Start()
    {
        base.Start();
        jumpsRemaining = maxJumps;

        // Pass MovementComponent to AnimationController
        if (_animationController != null && movementComponent != null)
        {
            _animationController.SetMovementComponent(movementComponent);
        }

        // Subscribe to input events
        if (_inputHandler != null)
        {
            _inputHandler.OnJumpPressed += Jump;
            _inputHandler.OnAttackPressed += Attack;
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

        // Update animation
        _animationController?.UpdateMovementAnimation();

        // Reset jumps when grounded
        if (movementComponent != null && movementComponent.IsGrounded())
        {
            jumpsRemaining = maxJumps;
        }
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        // Use input from InputHandler
        Vector2 moveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;
        Move(moveInput);
    }

    public override void Move(Vector2 direction)
    {
        if (!isAlive) return;

        movementComponent?.Move(direction);

        // Flip sprite based on direction
        if (direction.x != 0)
        {
            characterTransform.localScale = new Vector3(
                Mathf.Sign(direction.x),
                1,
                1
            );
        }
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
        if (!isAlive || jumpsRemaining <= 0) return;

        if (movementComponent == null)
        {
            _logger?.LogError("Cannot jump: MovementComponent is null");
            return;
        }

        // Reset Y velocity for consistent jump height
        Vector2 velocity = movementComponent.GetVelocity();
        movementComponent.SetVelocity(new Vector2(velocity.x, 0));

        // Add jump force
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        jumpsRemaining--;

        // Animation & Audio
        _animationController?.PlayJumpAnimation();
        _audioController?.PlayJumpSound();

        _logger?.Log($"Player jumped (jumps remaining: {jumpsRemaining})");
    }

    public void AddScore(int points)
    {
        score += points;
        _logger?.Log($"Score updated: {score}");
    }

    public int GetScore() => score;

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

    protected override void OnTakeDamage()
    {
        StartCoroutine(DamageFlash());
        _audioController?.PlayHurtSound();
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
        {
            sprite.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sprite.color = Color.white;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Cleanup
        if (_inputHandler != null)
        {
            _inputHandler.OnJumpPressed -= Jump;
            _inputHandler.OnAttackPressed -= Attack;
            _inputHandler.Dispose();
        }
    }
}