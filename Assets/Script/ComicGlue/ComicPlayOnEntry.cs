using UnityEngine;
using VContainer;
using Game.Persistence;
using Game.Rooms;

namespace Game.Comic
{
    /// <summary>
    /// Scene-placed comic that plays automatically the moment the room finishes loading —
    /// for intro/cutscene beats that shouldn't require the player to walk into a trigger
    /// collider first. Fires off <see cref="IRoomLoader.RoomEntered"/> instead of
    /// <c>OnTriggerEnter2D</c>; otherwise identical contract to <see cref="ComicTrigger"/>
    /// (playOnce/pauseGame, comicsSeen bookkeeping). Injected the same way — see
    /// <c>SceneLifetimeScope.Configure</c>'s <c>InjectAll&lt;ComicPlayOnEntry&gt;</c>.
    /// Room scenes load Single, so this component only ever exists for the one room it was
    /// placed in and is destroyed before the next room's RoomEntered could fire.
    /// </summary>
    public class ComicPlayOnEntry : MonoBehaviour
    {
        [SerializeField] private ComicSequenceAsset sequence;
        [Tooltip("Skip playback once SaveData.comicsSeen already contains this sequence's id.")]
        [SerializeField] private bool playOnce = true;
        [Tooltip("Hard-pause gameplay (Time.timeScale via the shared PauseStack) and disable player input while this comic plays.")]
        [SerializeField] private bool pauseGame = true;

        private SaveService _saveService;
        private IRoomLoader _roomLoader;
        private bool _subscribed;

        [Inject]
        public void Construct(SaveService saveService, IRoomLoader roomLoader)
        {
            _saveService = saveService;
            _roomLoader = roomLoader;
        }

        // Subscribe in Start (after [Inject] has run during the scope build) — RoomManager
        // waits a frame for the scene's LifetimeScope to build before firing RoomEntered, so
        // this is always subscribed in time to catch it, including the very first room.
        private void Start()
        {
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
            if (sequence == null) return;
            if (ComicPlayer.Instance.IsPlaying) return;
            if (playOnce && _saveService != null && _saveService.IsComicSeen(sequence.SequenceId))
                return;

            ComicPlayer.Instance.Play(sequence, OnComicFinished, pauseGame);
        }

        private void OnComicFinished()
        {
            if (playOnce && _saveService != null)
                _saveService.MarkComicSeen(sequence.SequenceId);
        }
    }
}
