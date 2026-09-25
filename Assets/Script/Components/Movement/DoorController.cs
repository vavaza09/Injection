using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Components.Movement
{
    public sealed class DoorController : MonoBehaviour
    {
        public enum DoorVisualState
        {
            Closed,
            Opening,
            Open
        }

        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private bool startClosed = true;

        [Header("State Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private GameObject lightObject;
        [SerializeField] private float openingDuration = 1f;

        private static readonly int OpenTrigger = Animator.StringToHash("Open");
        private static readonly int CloseTrigger = Animator.StringToHash("Close");

        private DoorVisualState _state = DoorVisualState.Closed;

        public DoorVisualState State => _state;

        // DoorController lives in the Game.Components.Movement asmdef, which (like every custom
        // asmdef) can never reference Assembly-CSharp — where SoundManager lives loose — so it
        // can't play its own opening SFX. These events let a loose Assembly-CSharp caller
        // (DoorView) own that instead, mirroring MovementComponent.Jumped/WallJumped/GrabStarted's
        // exact same shape for the identical reason.
        /// <summary>Raised once the door has actually settled open (end of the timed opening
        /// beat) — NOT raised by <see cref="SnapOpen"/>, which has no beat to signal the end of.</summary>
        public event Action Opened;
        /// <summary>Raised if the opening beat was cancelled instead of completing (room
        /// reload/destroy mid-opening) — a caller that started something for the duration of the
        /// opening beat (e.g. a looping SFX) must stop it here too, not only on <see cref="Opened"/>.</summary>
        public event Action OpeningCanceled;

        private void Start()
        {
            if (startClosed) ApplyClosed();

            // The door cutaway (DoorCutawaySystem) pauses the game (Time.timeScale = 0) for the
            // whole reveal — that's the point, the player is frozen specifically to watch this door
            // open. An Animator's default UpdateMode reads scaled Time.deltaTime, so without this it
            // would freeze on frame 0 for the entire cutaway and only actually animate afterward,
            // once nobody is watching. See the matching ignoreTimeScale on the UniTask.Delay below.
            if (animator != null)
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // Animated open: Closed -> Opening (light on, plays the Open animator trigger) -> Open
        // (settles: collider disabled, open sprite) after openingDuration. Used for a live switch
        // activation, so the player watches it happen during the door cutaway's hold window.
        public void Open()
        {
            if (_state != DoorVisualState.Closed) return;
            PlayOpeningAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PlayOpeningAsync(CancellationToken token)
        {
            _state = DoorVisualState.Opening;
            ApplyLight(true);
            if (animator != null)
                animator.SetTrigger(OpenTrigger);
            else if (visualRoot != null)
                visualRoot.SetActive(false);

            try
            {
                // ignoreTimeScale: true — see the comment on Start()'s AnimatorUpdateMode line; this
                // delay must keep progressing through the cutaway's Time.timeScale = 0 window too.
                await UniTask.Delay(TimeSpan.FromSeconds(openingDuration), ignoreTimeScale: true, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-opening (room reload) — scene is tearing down anyway.
                OpeningCanceled?.Invoke();
                return;
            }

            SettleOpen();
            Opened?.Invoke();
        }

        // Instant open: skips the Opening beat entirely — used when a door was already persisted
        // open on room load (nothing to watch happen).
        public void SnapOpen()
        {
            SettleOpen();
        }

        public void Close()
        {
            ApplyClosed();
            if (animator != null)
                animator.SetTrigger(CloseTrigger);
        }

        private void SettleOpen()
        {
            _state = DoorVisualState.Open;
            if (blockingCollider != null) blockingCollider.enabled = false;
            ApplySprite(openSprite);
            ApplyLight(true);
        }

        private void ApplyClosed()
        {
            _state = DoorVisualState.Closed;
            if (blockingCollider != null) blockingCollider.enabled = true;
            if (animator == null && visualRoot != null) visualRoot.SetActive(true);
            ApplySprite(closedSprite);
            ApplyLight(false);
        }

        private void ApplySprite(Sprite sprite)
        {
            // Explicit null checks, not ?. — an unassigned serialized Unity Object reference can
            // still throw UnassignedReferenceException through the null-conditional operator.
            if (spriteRenderer != null && sprite != null)
                spriteRenderer.sprite = sprite;
        }

        private void ApplyLight(bool on)
        {
            if (lightObject != null)
                lightObject.SetActive(on);
        }
    }
}
