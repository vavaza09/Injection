using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer;
using Game.Rooms;

namespace Game.UI
{
    /// <summary>
    /// Fades the current room's <see cref="RoomDefinition.displayName"/> in at the top of the
    /// screen the first time the player enters that room this session. Lives on the persistent
    /// PlayerHUD canvas. Modelled on <c>ComicPlayOnEntry</c> — same fire-on-RoomEntered contract —
    /// but keeps its own in-memory seen-set instead of touching SaveData, so a death reload
    /// (which re-fires RoomEntered for the same room) never replays the card, while a fresh
    /// launch of the game does. Rooms with a blank displayName (the tutorial) get no card.
    /// </summary>
    public class LevelNameCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI label;

        [Header("Timing (unscaled seconds)")]
        [SerializeField] private float delayBeforeShow = 0.2f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float holdDuration = 2f;
        [SerializeField] private float fadeOutDuration = 0.8f;

        private readonly HashSet<string> _seen = new();
        private IRoomLoader _roomLoader;
        private RoomCatalog _catalog;
        private Coroutine _routine;
        private bool _subscribed;

        [Inject]
        public void Construct(IRoomLoader roomLoader, RoomCatalog catalog)
        {
            _roomLoader = roomLoader;
            _catalog = catalog;
        }

        // Subscribe in Start (after [Inject]): RoomManager waits a frame for the scene scope to
        // build before firing RoomEntered, so this always catches it, including the first room.
        private void Start()
        {
            if (group != null) group.alpha = 0f;
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
            if (group == null || label == null) return;
            if (_catalog == null || !_catalog.TryGet(roomId, out var def)) return;
            if (string.IsNullOrEmpty(def.displayName)) return; // blank displayName = no card
            if (!_seen.Add(roomId)) return;                    // already shown this session

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(def.displayName));
        }

        private IEnumerator ShowRoutine(string text)
        {
            group.alpha = 0f;
            label.text = text;

            // RoomEntered fires while the screen is still covered by the fade / loading overlay
            // (both sort above PlayerHUD). Wait until the transition is done before animating.
            yield return new WaitUntil(() => _roomLoader == null || !_roomLoader.IsTransitioning);
            yield return new WaitForSecondsRealtime(delayBeforeShow);

            yield return Fade(0f, 1f, fadeInDuration);
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return Fade(1f, 0f, fadeOutDuration);

            _routine = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f) { group.alpha = to; yield break; }

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
