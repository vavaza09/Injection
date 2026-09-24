using UnityEngine;
using VContainer;
using Core.Logging;
using Game.Components.Movement;
using Game.Characters.Player;
using Game.Persistence;
using Game.Progression;

namespace Game.Components.Glide
{
    /// <summary>InputView-style adapter wiring Space to <see cref="GlideSystem"/>. Zero glide
    /// decision logic lives here — see <see cref="GlideSystem"/> for the rules; this only reads
    /// <see cref="MovementComponent"/>/<see cref="Player"/> state into a <see cref="GlideContext"/>,
    /// forwards the Jump input, and pushes the result back onto MovementComponent/Animator.
    ///
    /// <see cref="GlideModel"/>/<see cref="GlideSystem"/> are injected, NOT constructed here —
    /// they must be the same singleton instances <see cref="CompanionFollowerView"/> observes,
    /// registered once in RootLifetimeScope/DevSceneLifetimeScope.
    ///
    /// Sits on the same GameObject as <see cref="MovementComponent"/> (the Player). Player.cs
    /// itself is never touched — jump handling, gating, etc. are completely unaware this exists.
    /// </summary>
    [RequireComponent(typeof(MovementComponent))]
    public sealed class PlayerGlideController : MonoBehaviour
    {
        private Core.Logging.ILogger _logger;
        private MovementComponent _movementComponent;
        private Player _player;
        private PlayerInputHandler _inputHandler;
        private PlayerAnimationController _animationController;
        private SaveService _saveService;
        private GlideModel _model;
        private GlideSystem _system;

        // Snapshot taken at the END of the previous frame (LateUpdate), never a live read at
        // press time — see the "Fixed after critic review" note on the same-frame grab-launch
        // race in the feature brief for why this ordering matters.
        private GlideContext _lastFrameContext;

        [Inject]
        public void Construct(
            LoggerFactory loggerFactory,
            PlayerInputHandler inputHandler,
            PlayerAnimationController animationController,
            SaveService saveService,
            GlideModel model,
            GlideSystem system)
        {
            _logger = loggerFactory?.CreateLogger<PlayerGlideController>();
            _inputHandler = inputHandler;
            _animationController = animationController;
            _saveService = saveService;
            _model = model;
            _system = system;
        }

        private void Awake()
        {
            _movementComponent = GetComponent<MovementComponent>();
            _player = GetComponent<Player>();
        }

        private void Start()
        {
            _lastFrameContext = BuildContext();
            _model.StateChanged += OnGlideStateChanged;

            if (_inputHandler != null)
                _inputHandler.OnJumpPressed += OnJumpPressed;
        }

        private void OnDestroy()
        {
            if (_inputHandler != null)
                _inputHandler.OnJumpPressed -= OnJumpPressed;
            if (_model != null)
                _model.StateChanged -= OnGlideStateChanged;
        }

        private void Update()
        {
            _system.Tick(BuildContext());

            _movementComponent.FallSpeedLimitOverride = _system.FallSpeedCap;
            _movementComponent.FallSpeedLimitEaseRate = _system.FallSpeedCapEaseRate;
        }

        private void LateUpdate()
        {
            _lastFrameContext = BuildContext();
        }

        private void OnJumpPressed()
        {
            bool isUnlocked = _saveService != null && _saveService.IsAbilityUnlocked(AbilityIds.Glide);
            _system.RequestToggle(_lastFrameContext, isUnlocked);
        }

        private void OnGlideStateChanged(GlideState state, GlideEndReason reason)
        {
            _animationController?.SetGliding(state == GlideState.Gliding);
        }

        private GlideContext BuildContext()
        {
            return new GlideContext(
                isAirborne: _movementComponent.IsAirborne,
                canGroundJump: _movementComponent.CanGroundJump,
                isDashing: _movementComponent.IsDashing,
                isWallSliding: _movementComponent.IsWallSliding,
                isGrabbing: _movementComponent.IsGrabbing,
                isStunned: _movementComponent.IsStunned,
                isCaptured: _movementComponent.IsCaptured,
                isKnocked: _movementComponent.IsDamageKnocked,
                isAlive: _player == null || _player.IsAlive);
        }
    }
}
