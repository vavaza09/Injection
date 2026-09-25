using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Game.Comic;
using Game.Persistence;
using Game.Rooms;
using Game.Tutorial;
using Game.UI;

namespace Game.Progression
{
    /// <summary>
    /// Scene-placed component that permanently unlocks one <see cref="AbilityIds"/> ability the
    /// moment the room finishes loading, and shows a one-shot notice on the tutorial's
    /// <see cref="TutorialPromptUI"/> (same text-plus-glyph-chip UI, not a separate design).
    /// Fires off <see cref="IRoomLoader.RoomEntered"/> exactly like <see cref="ComicPlayOnEntry"/>
    /// — same subscribe-in-Start/unsubscribe-in-OnDestroy contract, since RoomManager waits a
    /// frame for the scene's LifetimeScope to build before firing RoomEntered either way.
    /// Do not place this in 0_TutorialLevel — TutorialManager drives the same TutorialPromptUI
    /// instance there and the two would fight over it.
    /// </summary>
    public sealed class AbilityUnlockOnEntry : MonoBehaviour
    {
        [SerializeField] private string abilityId = AbilityIds.Glide;
        [SerializeField] private TutorialPromptUI promptUI;
        [SerializeField, TextArea] private string message = "Ability unlocked!";
        [Tooltip("Action keys shown as button glyphs in the notice (resolved by TutorialPromptUI bindings).")]
        [SerializeField] private PromptEntry[] promptKeys;

        [Header("Timing (unscaled seconds)")]
        [SerializeField] private float delayBeforeShow = 0.3f;
        [SerializeField] private float displaySeconds = 3f;

        [SerializeField] private UnityEvent onUnlocked;

        private SaveService _saveService;
        private IRoomLoader _roomLoader;
        private Core.Logging.ILogger _logger;
        private LevelNameCard _nameCard;
        private bool _subscribed;

        [Inject]
        public void Construct(SaveService saveService, IRoomLoader roomLoader, Core.Logging.LoggerFactory loggerFactory)
        {
            _saveService = saveService;
            _roomLoader = roomLoader;
            _logger = loggerFactory?.CreateLogger<AbilityUnlockOnEntry>();
        }

        // Subscribe in Start (after [Inject] has run during the scope build) — RoomManager waits
        // a frame for the scene's LifetimeScope to build before firing RoomEntered, so this is
        // always subscribed in time to catch it, including the very first room. Same contract as
        // ComicPlayOnEntry/LevelNameCard.
        private void Start()
        {
            // LevelNameCard lives on the persistent PlayerHUD (DontDestroyOnLoad, injected once
            // by RootLifetimeScope) — not DI-resolvable itself, so it's looked up the same way
            // TutorialManager falls back to FindAnyObjectByType for TutorialPromptUI. Null in any
            // scene without a PlayerHUD (e.g. a DevScene) — the queue wait below just no-ops then.
            _nameCard = FindAnyObjectByType<LevelNameCard>();

            if (_roomLoader != null)
            {
                _roomLoader.RoomEntered += OnRoomEntered;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && _roomLoader != null)
                _roomLoader.RoomEntered -= OnRoomEntered;
        }

        private void OnRoomEntered(string roomId, string spawnPointId)
        {
            if (_saveService == null || string.IsNullOrEmpty(abilityId)) return;

            // Idempotent — a death reload or re-entering the room re-fires RoomEntered for the
            // same room, but the ability is already unlocked by then, so the notice never replays.
            if (_saveService.IsAbilityUnlocked(abilityId)) return;

            _saveService.MarkAbilityUnlocked(abilityId);
            onUnlocked?.Invoke();

            if (promptUI == null)
            {
                _logger?.LogWarning($"AbilityUnlockOnEntry unlocked '{abilityId}' but has no promptUI assigned — skipping notice.");
                return;
            }

            ShowNoticeAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid ShowNoticeAsync(CancellationToken token)
        {
            try
            {
                // RoomEntered fires while the screen is still covered by the fade/loading overlay.
                await UniTask.WaitUntil(() => _roomLoader == null || !_roomLoader.IsTransitioning, cancellationToken: token);

                // If this room's LevelNameCard is showing (same RoomEntered event, same frame),
                // queue behind it — our notice shows after the room title card is done, not on
                // top of it.
                if (_nameCard != null)
                    await UniTask.WaitUntil(() => !_nameCard.IsShowing, cancellationToken: token);

                // If this room also plays a ComicPlayOnEntry sequence off the same RoomEntered
                // event, let it finish first rather than overlapping the notice on top of it.
                await UniTask.WaitUntil(() => !ComicPlayer.Instance.IsPlaying, cancellationToken: token);

                await UniTask.Delay(TimeSpan.FromSeconds(delayBeforeShow), ignoreTimeScale: true, cancellationToken: token);

                promptUI.Show(message, promptKeys);

                await UniTask.Delay(TimeSpan.FromSeconds(displaySeconds), ignoreTimeScale: true, cancellationToken: token);

                promptUI.Hide();
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-notice (room reload/teardown) — nothing left to hide.
            }
        }
    }
}
