using UnityEngine;
using VContainer;
using Core.Logging;
using Game.Characters.Player;

namespace Game.Components.Glide
{
    /// <summary>Pure View: trails the player in WORLD SPACE and hides while gliding. Deliberately
    /// a SIBLING GameObject to the Player (same DontDestroyOnLoad root), never a child of the
    /// Player transform — parenting under Player would inherit its facing localScale flip
    /// (Player.cs mirrors the sprite by flipping localScale.x) and have its motion swallowed by
    /// the parent's interpolated Rigidbody before LateUpdate runs, canceling out the trailing lag
    /// this is meant to show. See the "Fixed after critic review" note in the feature brief.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CompanionFollowerView : MonoBehaviour
    {
        [Header("Follow")]
        [Tooltip("Offset from the player, mirrored on the player's facing direction so the " +
                 "companion trails behind rather than sitting on a fixed side.")]
        [SerializeField] private Vector2 offset = new Vector2(-1f, 1f);
        [SerializeField] private float smoothTime = 0.25f;
        [Tooltip("Distance beyond which the companion snaps instead of smoothing — covers " +
                 "teleports (respawn, room load) ONLY. Must stay well above any legitimate " +
                 "single-frame displacement or normal fast movement (Dash is 240 u/s — even at " +
                 "60fps that's ~4 units/frame, more under a frame hitch) will falsely trigger " +
                 "snap-every-frame instead of smoothing, which looks like fast, wide jitter " +
                 "rather than a trail. Keep this an order of magnitude above that, not close to it.")]
        [SerializeField] private float snapDistance = 20f;

        [Header("Bob")]
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobSpeed = 2f;

        private Core.Logging.ILogger _logger;
        private Player _player;
        private GlideModel _model;
        private SpriteRenderer _spriteRenderer;
        private Vector2 _dampVelocity; // Vector2.SmoothDamp's ref velocity, not a Rigidbody one
        private float _bobPhase;
        private bool _isReady;

        [Inject]
        public void Construct(LoggerFactory loggerFactory, Player player, GlideModel model)
        {
            _logger = loggerFactory?.CreateLogger<CompanionFollowerView>();
            _player = player;
            _model = model;
        }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (_player == null)
            {
                _logger?.LogError("CompanionFollowerView: Player not injected — disabling.");
                enabled = false;
                return;
            }

            transform.position = TargetPosition();
            _model.StateChanged += OnGlideStateChanged;
            _isReady = true;
        }

        private void OnDestroy()
        {
            if (_model != null)
                _model.StateChanged -= OnGlideStateChanged;
        }

        private void LateUpdate()
        {
            if (!_isReady) return;

            // Sprite mirrors the player's current facing directly (not smoothed/lagged like
            // position) — a gradually-flipping sprite would look broken, only the trail should lag.
            _spriteRenderer.flipX = GetFacingSign() < 0f;

            _bobPhase += Time.deltaTime * bobSpeed;
            Vector2 target = TargetPosition();
            Vector2 current = transform.position;

            if (Vector2.Distance(current, target) > snapDistance)
            {
                transform.position = target;
                _dampVelocity = Vector2.zero;
                return;
            }

            Vector2 smoothed = Vector2.SmoothDamp(current, target, ref _dampVelocity, smoothTime);
            smoothed.y += Mathf.Sin(_bobPhase) * bobAmplitude;
            transform.position = new Vector3(smoothed.x, smoothed.y, transform.position.z);
        }

        // Player.cs mirrors its sprite by flipping localScale.x (positive = facing right) —
        // mirror that same convention here rather than reading an unrelated signal.
        private float GetFacingSign() => Mathf.Sign(
            _player.transform.localScale.x != 0f ? _player.transform.localScale.x : 1f);

        private Vector2 TargetPosition()
        {
            Vector2 facedOffset = new Vector2(offset.x * GetFacingSign(), offset.y);
            return (Vector2)_player.transform.position + facedOffset;
        }

        private void OnGlideStateChanged(GlideState state, GlideEndReason reason)
        {
            _spriteRenderer.enabled = state == GlideState.Following;
        }
    }
}
