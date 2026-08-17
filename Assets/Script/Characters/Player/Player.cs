using UnityEngine;
using Game.Characters.Player;
using Core.Logging;
using VContainer;
using Game.UI.Movement;
using Game.Tutorial;
using System.Collections;
using System.Collections.Generic;

public enum PlayerState
{
    Dead        = 0,
    Dashing     = 1,
    Attacking   = 2,
    WallSliding = 3,
    Jumping     = 4,
    Falling     = 5,
    Running     = 6,
    Idle        = 7,
    Grabbing    = 8,
    Knockback   = 9
}

public class Player : character
{
    #region Fields

    private Core.Logging.ILogger _logger;

    [Header("Player Components")]
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animationController;
    private PlayerAudioController _audioController;
    private ISlowMotionController _slowMotion;

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
    [SerializeField, Range(0f, 1f)] private float dashAnimUpThreshold = 0.5f;
    [SerializeField, Range(-1f, 0f)] private float dashAnimDownThreshold = -0.5f;

    [Header("References for DI")]
    [SerializeField] private Animator animator;

    [Header("Health Settings")]
    [SerializeField] private float invincibilityDuration = 2f;

    [Header("Health Regeneration")]
    [SerializeField] private bool enableHealthRegen = true;
    [SerializeField] private float regenDelay = 5f;
    [SerializeField] private float regenInterval = 1f;
    private float _timeSinceLastDamage = 0f;
    private float _regenTickTimer = 0f;

    [Header("State Machine")]
    [SerializeField] private float attackStateDuration = 0.5f;

    private PlayerState _currentState  = PlayerState.Idle;
    private PlayerState _previousState = PlayerState.Idle;
    private float       _stateTimer    = 0f;

    [Header("Movement Combat")]
    [SerializeField] private PlayerDashImpact dashImpact;

    [Header("Movement VFX")]
    [SerializeField] private PlayerMovementVFX movementVFX;

    private PlayerEnergyCollector _energyCollector;
    private bool _wasDownHeld;

    // Tutorial ability gating. Inactive by default — in normal gameplay every ability is
    // available. The tutorial calls SetAbilityGating(true) then unlocks abilities one step at a
    // time. Walk/jump are never gated (no keys for them).
    private readonly HashSet<string> _unlockedAbilities = new HashSet<string>();
    private bool _abilityGatingActive;

    [Header("Damage Feedback")]
    [SerializeField] private PlayerDamageFeedback _damageFeedback;

    [Header("Aim Feedback")]
    [SerializeField] private PlayerAimFeedback _aimFeedback;

    [Header("Invincibility Visual")]
    [SerializeField] private float invincibilityBlinkInterval = 0.1f;
    private SpriteRenderer _spriteRenderer;
    private Coroutine _invincibilityBlinkCoroutine;

    #endregion

    #region Dependency Injection

    [Inject]
    public void Construct(
        LoggerFactory loggerFactory,
        PlayerInputHandler inputHandler,
        PlayerAnimationController animationController,
        PlayerAudioController audioController,
        ISlowMotionController slowMotion)
    {
        _logger = loggerFactory?.CreateLogger<Player>();
        _inputHandler = inputHandler;
        _animationController = animationController;
        _audioController = audioController;
        _slowMotion = slowMotion;
        _logger?.Log("Player components injected via DI");
    }

    #endregion

    #region Tutorial Ability Gating

    /// <summary>True if the ability may be used. When gating is inactive (normal gameplay) this
    /// is always true. Walk/jump are never passed here — they are never gated.</summary>
    public bool IsAbilityUnlocked(string ability)
        => !_abilityGatingActive || _unlockedAbilities.Contains(ability);

    /// <summary>Turn the tutorial gate on/off. When turned on, every gated ability starts locked
    /// until <see cref="UnlockAbility"/> is called for it.</summary>
    public void SetAbilityGating(bool active)
    {
        _abilityGatingActive = active;
        if (active) _unlockedAbilities.Clear();
        RefreshMovementGates();
    }

    /// <summary>Unlock a single ability (see <see cref="TutorialAbilities"/> for keys).</summary>
    public void UnlockAbility(string ability)
    {
        if (string.IsNullOrEmpty(ability)) return;
        _unlockedAbilities.Add(ability);
        RefreshMovementGates();

        // Edge case: if the player is already holding right-click when dash unlocks,
        // BeginDashAimMode already fired and returned early (before unlock). Re-activate now.
        if (ability == TutorialAbilities.Dash && _inputHandler != null && _inputHandler.IsAimHeld)
            ActivateSlowMotion();
    }

    /// <summary>Unlock everything and disable gating (used when the tutorial completes).</summary>
    public void UnlockAllAbilities()
    {
        _abilityGatingActive = false;
        RefreshMovementGates();
    }

    public void SetInputEnabled(bool enabled)
    {
        if (_inputHandler == null) return;
        if (enabled) _inputHandler.Enable(); else _inputHandler.Disable();
    }

    // Wall-slide/jump and auto-grab live inside MovementComponent and run automatically, so they
    // can't be gated by an early-return at a call site — push the current lock state down to it.
    private void RefreshMovementGates()
    {
        if (movementComponent == null) return;

        if (_abilityGatingActive)
        {
            movementComponent.SetWallEnabled(_unlockedAbilities.Contains(TutorialAbilities.Wall));
            movementComponent.AutoGrab = _unlockedAbilities.Contains(TutorialAbilities.Grab);
        }
        else
        {
            // Restore normal gameplay defaults.
            movementComponent.SetWallEnabled(true);
            movementComponent.AutoGrab = true;
        }
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        if (_damageFeedback == null)
            _damageFeedback = GetComponent<PlayerDamageFeedback>();

        if (_aimFeedback == null)
            _aimFeedback = GetComponent<PlayerAimFeedback>();

        if (dashImpact == null)
            dashImpact = GetComponent<PlayerDashImpact>();

        if (movementVFX == null)
            movementVFX = GetComponent<PlayerMovementVFX>();

        _energyCollector = GetComponent<PlayerEnergyCollector>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            _logger?.LogWarning("Animator not assigned, finding in children...");
        }

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        TryAutoAssignDashAimDisplay();
    }

    protected override void Start()
    {
        base.Start();

        TryAutoAssignDashAimDisplay();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (_inputHandler != null)
        {
            _inputHandler.SetCamera(mainCamera);
        }

        if (_animationController != null && movementComponent != null)
        {
            _animationController.SetMovementComponent(movementComponent);
            _animationController.SetDashThresholds(dashAnimUpThreshold, dashAnimDownThreshold);
        }

        if (_inputHandler != null)
        {
            _inputHandler.OnJumpPressed += Jump;
            _inputHandler.OnJumpReleased += CancelJump;
            _inputHandler.OnRightClickPressed += BeginDashAimMode;
            _inputHandler.OnRightClickReleased += EndDashAimMode;
            _inputHandler.OnDashPressed += Dash;
            _inputHandler.OnGrabPressed += HandleGrabInput;

            _inputHandler.Enable();
        }
        else
        {
            _logger?.LogError("PlayerInputHandler not injected!");
        }

        InvincibilityStarted += OnInvincibilityVisualStart;
        InvincibilityEnded   += OnInvincibilityVisualEnd;

        movementVFX?.Initialize(movementComponent);
        if (movementVFX != null)
            movementVFX.Landed += OnLanded;

        if (movementComponent != null)
        {
            movementComponent.Jumped += OnJumped;
            movementComponent.WallJumped += OnWallJumped;
        }

        if (movementComponent != null)
        {
            movementComponent.GrabStarted += OnGrabStarted;
        }

        if (dashImpact != null)
        {
            dashImpact.Initialize(movementComponent);
            dashImpact.ImpactLanded += OnDashImpactLanded;
        }

        // Re-apply any tutorial gating now that movementComponent is initialized — Start() ordering
        // between this and TutorialManager is undefined, so SetAbilityGating() may have run before
        // movementComponent existed.
        RefreshMovementGates();

        if (animator != null)
        {
            var relay = animator.GetComponent<PlayerFootstepRelay>();
            if (relay == null) relay = animator.gameObject.AddComponent<PlayerFootstepRelay>();
            relay.SetCallback(() =>
                _audioController?.PlayFootstepSound(
                    movementComponent != null ? movementComponent.GetGroundTag() : null));
        }

        _logger?.Log("Player initialized");
    }

    protected override void Update()
    {
        base.Update();

        // Re-bind camera after scene transitions (old camera is destroyed, mainCamera goes null).
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
                _inputHandler?.SetCamera(mainCamera);
        }

        _inputHandler?.UpdateAimDirection(transform);
        if (_inputHandler != null)
        {
            CameraController.instance?.SetAim(_inputHandler.IsAimHeld, _inputHandler.AimWorldOffset);
            CinemachineAimLean.instance?.SetAim(_inputHandler.IsAimHeld, _inputHandler.AimWorldOffset);
            _aimFeedback?.SetAiming(_inputHandler.IsAimHeld);
        }
        UpdateDashAimUI();

        _stateTimer += Time.deltaTime;
        EvaluateStateTransitions();

        if (_currentState == PlayerState.Dead) return;

        UpdateHealthRegen();
        _animationController?.UpdateMovementAnimation();

        if (movementComponent != null && movementComponent.IsGrounded())
        {
            jumpsRemaining = maxJumps;
        }

    }

    #endregion

    #region Physics Update

    private void FixedUpdate()
    {
        if (_currentState == PlayerState.Dead) return;

        Vector2 moveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;

        bool isDownHeld = moveInput.y < -0.5f;
        if (isDownHeld && !_wasDownHeld && movementComponent != null)
            movementComponent.DropThroughPlatform();
        _wasDownHeld = isDownHeld;

        Move(moveInput);
    }

    #endregion

    #region Movement And Actions

    public override void Move(Vector2 direction)
    {
        if (_currentState == PlayerState.Dead) return;

        movementComponent?.Move(direction);

        // While wall sliding/hanging the sprite must face INTO the wall and cannot be
        // flipped by horizontal input. WallSideSign is +1 when the wall is on the right,
        // -1 when on the left, which maps directly onto localScale.x (face toward the wall).
        if (movementComponent != null && movementComponent.IsWallSliding && movementComponent.WallSideSign != 0)
        {
            characterTransform.localScale = new Vector3(
                movementComponent.WallSideSign,
                1,
                1
            );
            return;
        }

        if (direction.x != 0)
        {
            characterTransform.localScale = new Vector3(
                Mathf.Sign(direction.x),
                1,
                1
            );
        }
    }

    private void HandleGrabInput()
    {
        if (_currentState == PlayerState.Dead) return;
        if (movementComponent == null) return;

        if (_energyCollector != null && _energyCollector.TryCollectNearby())
        {
            _audioController?.PlayEnergyPickupSound();
            return;
        }

        if (!IsAbilityUnlocked(TutorialAbilities.Grab)) return;

        if (movementComponent.IsGrabbing)
        {
            movementComponent.ReleaseGrab();
            _slowMotion?.StopSlowMotion();
            return;
        }

        // Block grab while right-click dash-aim is held
        if (_inputHandler != null && _inputHandler.IsAimHeld) return;

        if (movementComponent.CanGrab)
        {
            movementComponent.TryStartGrab();
        }
    }

    private void Dash()
    {
        if (_currentState == PlayerState.Dead) return;
        if (movementComponent == null) return;
        if (!IsAbilityUnlocked(TutorialAbilities.Dash)) return;

        if (requireAimHoldForDash && (_inputHandler == null || !_inputHandler.IsAimHeld))
        {
            _logger?.Log("Dash blocked: hold right click to enter aim slow-motion mode first.");
            return;
        }

        _logger?.Log("Dash initiated");
        Vector2 aimDir = ResolveDashDirection();
        movementComponent.Dash(aimDir);
        // Sound and hit-registry clear fire in OnStateEnter(Dashing) when IsDashing becomes true.
    }

    public Vector2 MoveInput => _inputHandler?.MoveInput ?? Vector2.zero;

    public bool IsAimingDash => isAlive && _inputHandler != null && _inputHandler.IsAimHeld
                                && IsAbilityUnlocked(TutorialAbilities.Dash);

    public void Jump()
    {
        if (_currentState == PlayerState.Dead) return;

        if (movementComponent == null)
        {
            _logger?.LogError("Cannot jump: MovementComponent is null");
            return;
        }

        if (movementComponent.IsGrabbing)
        {
            Vector2 aimDir = ResolveDashDirection();
            movementComponent.LaunchFromGrab(aimDir);
            _slowMotion?.StopSlowMotion();
            jumpsRemaining = maxJumps;
            return;
        }

        movementComponent.Jump(particles: true, playSfx: true);
    }

    public void CancelJump()
    {
        if (_currentState == PlayerState.Dead) return;
        movementComponent?.CancelJump();
    }

    // One-shot jump feedback. Driven by MovementComponent events (fired the instant a jump
    // launches) instead of the polled state machine, so sfx/vfx are never lost to frame timing.
    private void OnJumped()
    {
        _audioController?.PlayJumpSound();
        movementVFX?.PlayJumpPuff();
    }

    private void OnWallJumped()
    {
        _audioController?.PlayJumpSound();
        movementVFX?.PlayWallJumpBurst();
    }

    private void OnLanded()
    {
        _audioController?.PlayLandSound();
    }

    private void OnGrabStarted()
    {
        _audioController?.PlayGrabSound();
        ActivateSlowMotion();
    }

    // Dash impact landed on an enemy — either a normal dash hitting a weak point or a True-Damage
    // dash hitting any enemy. Refresh the dash so the player can chain straight into the next target
    // mid-air (combo), and re-arm the slow-mo aim window so they get a fresh moment to aim.
    private void OnDashImpactLanded()
    {
        ChangeState(PlayerState.Attacking);
        if (movementComponent != null)
            movementComponent.RechargeDashForCombo();
        ActivateSlowMotion();
    }

    private void ActivateSlowMotion()
    {
        if (_currentState == PlayerState.Dead) return;

        if (useSmoothSlowMotion)
        {
            _slowMotion?.StartSlowMotionSmooth(
                slowMotionTimeScale,
                slowMotionDuration,
                easeInDuration: 0.1f,
                easeOutDuration: 0.3f
            );
        }
        else
        {
            _slowMotion?.StartSlowMotion(
                slowMotionTimeScale,
                slowMotionDuration
            );
        }

        _logger?.Log($"Slow Motion activated! TimeScale: {slowMotionTimeScale}, Duration: {slowMotionDuration}s");
    }

    private void BeginDashAimMode()
    {
        if (_currentState == PlayerState.Dead) return;
        if (!IsAbilityUnlocked(TutorialAbilities.Dash)) return;
        _audioController?.PlaySlowMoSound();
        ActivateSlowMotion();
    }

    private void EndDashAimMode()
    {
        _slowMotion?.StopSlowMotion();
    }

    private void UpdateDashAimUI()
    {
        if (dashAimDisplay == null)
        {
            TryAutoAssignDashAimDisplay();
        }

        if (dashAimDisplay == null)
        {
            return;
        }

        bool isGrabbing = movementComponent != null && movementComponent.IsGrabbing;
        bool isAimHeld = isAlive && (
            isGrabbing ||
            (_inputHandler != null && _inputHandler.IsAimHeld && IsAbilityUnlocked(TutorialAbilities.Dash))
        );
        bool canDashNow = isGrabbing || (movementComponent != null && movementComponent.CanDash);
        bool isDashingNow = movementComponent != null && movementComponent.IsDashing;

        dashAimDisplay.Refresh(ResolveDashDirection(), isAimHeld, canDashNow, isDashingNow);
    }

    private void TryAutoAssignDashAimDisplay()
    {
        if (dashAimDisplay != null)
        {
            return;
        }

        dashAimDisplay = GetComponentInChildren<DashAimDirectionDisplay>(includeInactive: true);
        if (dashAimDisplay != null)
        {
            _logger?.LogWarning("DashAimDirectionDisplay was not assigned and has been auto-bound from children.");
        }
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

    public override void Attack() { }

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

    public void StartCheatInvincibility(float duration)
    {
        healthComponent?.StartInvincibility(duration);
    }

    public void CheatRevive(float invincDuration)
    {
        if (healthComponent == null) return;
        healthComponent.Heal(healthComponent.maxHealth);
        movementComponent?.ResetState();
        jumpsRemaining = maxJumps;
        _timeSinceLastDamage = 0f;
        _regenTickTimer = 0f;
        if (!isAlive)
        {
            isAlive = true;
            ChangeState(PlayerState.Idle);
        }
        healthComponent.StartInvincibility(invincDuration);
    }

    public void HealToFull()
    {
        if (healthComponent != null)
            healthComponent.Heal(healthComponent.maxHealth);
    }

    public void Respawn(float invincDuration)
    {
        if (healthComponent != null)
            healthComponent.Heal(healthComponent.maxHealth);

        movementComponent?.ResetState();
        jumpsRemaining = maxJumps;
        _timeSinceLastDamage = 0f;
        _regenTickTimer = 0f;

        if (!isAlive)
        {
            isAlive = true;
            ChangeState(PlayerState.Idle);
        }

        if (healthComponent != null)
            healthComponent.StartInvincibility(invincDuration);
    }

    private void UpdateHealthRegen()
    {
        if (!enableHealthRegen || healthComponent == null) return;
        if (healthComponent.currentHealth >= healthComponent.maxHealth) return;

        _timeSinceLastDamage += Time.deltaTime;
        if (_timeSinceLastDamage < regenDelay) return;

        _regenTickTimer += Time.deltaTime;
        if (_regenTickTimer >= regenInterval)
        {
            _regenTickTimer -= regenInterval;
            Heal(1f);
        }
    }

    protected override void OnTakeDamage()
    {
        _timeSinceLastDamage = 0f;
        _regenTickTimer = 0f;
        movementComponent?.NotifyDamageTaken();
        healthComponent?.StartInvincibility(invincibilityDuration);
        _audioController?.PlayHurtSound();
        _damageFeedback?.PlayDamageFeedback(invincibilityDuration);
    }

    public void ApplyKnockback(Vector2 sourcePosition, float force, float upwardBias)
    {
        if (_currentState == PlayerState.Dead) return;
        movementComponent?.ApplyKnockback(sourcePosition, force, upwardBias);
        ChangeState(PlayerState.Knockback);
    }

    public bool IsOnPlatform
    {
        get
        {
            // Primary: use MovementComponent ground tag if available.
            if (movementComponent != null && movementComponent.GetGroundTag() == "platform")
                return true;
            // Fallback: small overlap at feet against the Platform layer (11).
            var col = GetComponent<Collider2D>();
            Vector2 feet = col != null
                ? new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f)
                : (Vector2)transform.position;
            return Physics2D.OverlapCircle(feet, 0.2f, 1 << 11) != null;
        }
    }

    public void SetCaptured(bool captured)
    {
        if (_currentState == PlayerState.Dead) return;
        movementComponent?.SetCaptured(captured);
    }

    public void DriveCapturedPosition(Vector2 worldPos)
    {
        movementComponent?.DriveCapturedPosition(worldPos);
    }

    public void Stun(float duration)
    {
        if (_currentState == PlayerState.Dead) return;
        movementComponent?.Stun(duration);
    }

    private void OnInvincibilityVisualStart()
    {
        if (_invincibilityBlinkCoroutine != null)
            StopCoroutine(_invincibilityBlinkCoroutine);
        _invincibilityBlinkCoroutine = StartCoroutine(InvincibilityBlinkCoroutine());
    }

    private void OnInvincibilityVisualEnd()
    {
        if (_invincibilityBlinkCoroutine != null)
        {
            StopCoroutine(_invincibilityBlinkCoroutine);
            _invincibilityBlinkCoroutine = null;
        }
        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;
    }

    private IEnumerator InvincibilityBlinkCoroutine()
    {
        while (true)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = !_spriteRenderer.enabled;
            yield return new WaitForSeconds(invincibilityBlinkInterval);
        }
    }

    protected override void OnDeath()
    {
        ChangeState(PlayerState.Dead);
    }

    #endregion

    #region State Machine

    private void ChangeState(PlayerState newState)
    {
        if (newState == _currentState) return;
        if (_currentState == PlayerState.Dead && newState != PlayerState.Idle) return;

        OnStateExit(_currentState);
        _previousState = _currentState;
        _currentState  = newState;
        _stateTimer    = 0f;
        OnStateEnter(_currentState);

        _logger?.Log($"[SM] {_previousState} → {_currentState}");
    }

    private void OnStateEnter(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Dead:
                _slowMotion?.ResetImmediate();
                movementComponent?.ReleaseGrab();
                movementComponent?.SetCanMove(false);
                if (movementComponent != null)
                {
                    var v = movementComponent.GetVelocity();
                    movementComponent.SetVelocity(new Vector2(0f, v.y));
                }
                _animationController?.PlayDeathAnimation();
                _audioController?.PlayDeathSound();
                _logger?.LogWarning("Player died! Game Over!");
                break;

            case PlayerState.Dashing:
                _audioController?.PlayDashSound();
                _slowMotion?.StopSlowMotion();
                CameraController.instance?.CancelAim();
                CinemachineAimLean.instance?.CancelAim();
                break;

            case PlayerState.Attacking:
                _animationController?.PlayAttackAnimation();
                _audioController?.PlayAttackSound();
                break;

            // Jump feedback (sfx + puff/wall-burst) is handled by OnJumped/OnWallJumped,
            // wired to MovementComponent's Jumped/WallJumped events — see Start().

            case PlayerState.WallSliding:
                _audioController?.StartWallSlideLoop();
                break;

            case PlayerState.Grabbing:
                // Placeholder: wire grab animation when art is ready
                break;

            case PlayerState.Knockback:
                if (movementComponent != null && movementComponent.IsDamageKnocked)
                    _animationController?.PlayKnockbackAnimation();
                // IsBounceKnocked: let IsFalling bool drive Fall animation naturally
                break;
        }
    }

    private void OnStateExit(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Dead:
                break;

            case PlayerState.WallSliding:
                _audioController?.StopWallSlideLoop();
                break;
        }
    }

    private void EvaluateStateTransitions()
    {
        if (!isAlive)
        {
            if (_currentState != PlayerState.Dead)
                ChangeState(PlayerState.Dead);
            return;
        }

        if (movementComponent != null && movementComponent.IsKnocked)
        {
            if (_currentState != PlayerState.Knockback)
                ChangeState(PlayerState.Knockback);
            return;
        }

        if (movementComponent != null && movementComponent.IsDashing)
        {
            if (_currentState != PlayerState.Dashing && _currentState != PlayerState.Attacking)
                ChangeState(PlayerState.Dashing);
            return;
        }

        if (movementComponent != null && (movementComponent.IsGrabbing || movementComponent.IsCaptured || movementComponent.IsStunned))
        {
            ChangeState(PlayerState.Grabbing);
            return;
        }

        if (_currentState == PlayerState.Attacking && _stateTimer < attackStateDuration)
            return;

        if (movementComponent == null) return;

        if (movementComponent.IsWallSliding)
        {
            ChangeState(PlayerState.WallSliding);
            return;
        }

        if (movementComponent.IsJumping())
        {
            ChangeState(PlayerState.Jumping);
            return;
        }

        if (movementComponent.IsFalling())
        {
            ChangeState(PlayerState.Falling);
            return;
        }

        if (movementComponent.IsGrounded())
        {
            const float runThreshold = 0.5f;
            ChangeState(Mathf.Abs(movementComponent.GetVelocity().x) > runThreshold
                ? PlayerState.Running
                : PlayerState.Idle);
        }
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
            _inputHandler.OnRightClickPressed -= BeginDashAimMode;
            _inputHandler.OnRightClickReleased -= EndDashAimMode;
            _inputHandler.OnDashPressed -= Dash;
            _inputHandler.OnGrabPressed -= HandleGrabInput;
            _inputHandler.Dispose();
        }

        if (movementVFX != null)
            movementVFX.Landed -= OnLanded;

        if (movementComponent != null)
        {
            movementComponent.Jumped -= OnJumped;
            movementComponent.WallJumped -= OnWallJumped;
            movementComponent.GrabStarted -= OnGrabStarted;
        }

        if (dashImpact != null)
        {
            dashImpact.ImpactLanded -= OnDashImpactLanded;
        }

        InvincibilityStarted -= OnInvincibilityVisualStart;
        InvincibilityEnded   -= OnInvincibilityVisualEnd;

        _slowMotion?.StopSlowMotion();
    }

    #endregion
}