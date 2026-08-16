using UnityEngine;
using VContainer;
using Game.Persistence;

namespace Game.Comic
{
    /// <summary>
    /// Scene-placed entry point for a comic: a designer drops this on a trigger collider, picks
    /// a <see cref="ComicSequenceAsset"/>, and it plays via <see cref="ComicPlayer"/> when the
    /// player walks in. Injected the same way as <c>SavePointTrigger</c>/<c>RoomPortal</c> — see
    /// <c>SceneLifetimeScope.Configure</c>, which explicitly injects every scene-placed trigger
    /// type via <c>InjectAll&lt;T&gt;</c> since nothing else resolves them.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ComicTrigger : MonoBehaviour
    {
        [SerializeField] private ComicSequenceAsset sequence;
        [Tooltip("Skip playback (and the trigger stays inert) once SaveData.comicsSeen already contains this sequence's id.")]
        [SerializeField] private bool playOnce = true;
        [Tooltip("Hard-pause gameplay (Time.timeScale via the shared PauseStack) and disable player input while this comic plays.")]
        [SerializeField] private bool pauseGame = true;

        private SaveService _saveService;

        [Inject]
        public void Construct(SaveService saveService)
        {
            _saveService = saveService;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
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
