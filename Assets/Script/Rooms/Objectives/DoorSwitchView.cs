using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Game.Components.Interaction;
using Game.Characters.Player;
using Game.UI;

namespace Game.Rooms.Objectives
{
    public enum SwitchState
    {
        Closed,
        Opening,
        On
    }

    /// <summary>
    /// A switch/lever the player interacts with (E) to open a linked door elsewhere in the room.
    /// Proximity trigger shows a world-space prompt and registers with InteractionSystem while the
    /// player is in range — mirrors EnergyPickup's proximity shape (Components/Skills/EnergyPickup.cs).
    /// Three visual/logical states: Closed -> Opening (timed, input locked) -> On (door + cutaway fire).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DoorSwitchView : MonoBehaviour, IInteractable
    {
        [SerializeField] private string switchId;
        [SerializeField] private string doorId;
        [SerializeField] private float interactRange = 1.5f;
        [SerializeField] private float openingDuration = 1f;
        [SerializeField] private GameObject interactPrompt;
        [SerializeField] private Animator animator;

        [Header("State Sprites")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openingSprite;
        [SerializeField] private Sprite onSprite;

        

        [Header("State Light")]
        [SerializeField] private GameObject lightObject;
        [SerializeField] private GameObject batteryObject;

        private static readonly int OpeningTrigger = Animator.StringToHash("Opening");
        private static readonly int OnTrigger = Animator.StringToHash("On");

        private DoorObjectiveSystem _system;
        private InteractionSystem _interactionSystem;
        private PlayerAnimationController _animationController;
        private IDoorObjectiveEvents _doorEvents;
        private SwitchState _state = SwitchState.Closed;
        private Coroutine _promptAnim;
        private AudioSource _openingSoundInstance;

        public Transform InteractTransform => transform;
        public float InteractRange => interactRange;
        public SwitchState State => _state;

        [Inject]
        public void Construct(
            DoorObjectiveSystem system,
            InteractionSystem interactionSystem,
            PlayerAnimationController animationController,
            IDoorObjectiveEvents doorEvents)
        {
            _system = system;
            _interactionSystem = interactionSystem;
            _animationController = animationController;
            _doorEvents = doorEvents;
            _doorEvents.DoorCutawayFinished += OnDoorCutawayFinished;
        }

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;

            if (interactPrompt != null)
                interactPrompt.SetActive(false);

            ApplySprite(SwitchState.Closed);
            ApplyLight(SwitchState.Closed);
            ApplyBattery(SwitchState.Closed);
        }

        private void Start()
        {
            if (_system != null && _system.IsDoorPersistedOpen(doorId))
                SetActivatedImmediate();
        }

        public void Interact()
        {
            if (_state != SwitchState.Closed) return;
            PlayOpeningAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PlayOpeningAsync(CancellationToken token)
        {
            _state = SwitchState.Opening;
            HidePrompt();
            ApplySprite(SwitchState.Opening);
            ApplyLight(SwitchState.Opening);
            ApplyBattery(SwitchState.Opening);
            if (animator != null)
                animator.SetTrigger(OpeningTrigger);

            PlayerInputGate.Set(false);
            _animationController?.SetInteracting(true);
            _openingSoundInstance = SoundManager.StartInstance(SoundType.DOOR_SWITCH_OPENING);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(openingDuration), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-opening (room reload) — scene is tearing down anyway, nothing to
                // unlock, but the Player persists across the reload, so its Animator would
                // otherwise be stuck showing the interact pose forever.
                _animationController?.SetInteracting(false);
                SoundManager.StopInstance(_openingSoundInstance);
                return;
            }

            PlayerInputGate.Set(true);
            _animationController?.SetInteracting(false);
            SoundManager.StopInstance(_openingSoundInstance);
            _state = SwitchState.On;
            ApplySprite(SwitchState.On);
            ApplyLight(SwitchState.On);
            // Battery deliberately NOT turned off here — it stays lit through DoorCutawaySystem's
            // camera cutaway and only clears in OnDoorCutawayFinished, once the camera has
            // actually switched back. Unlike the light, it shouldn't disappear the instant the
            // switch itself finishes, only once the player can see the door is open.

            if (animator != null)
                animator.SetTrigger(OnTrigger);

            _system?.ActivateSwitch(switchId, doorId);
        }

        // Called by DoorSwitchView.Start() when this switch's door was already persisted open on
        // room load — snaps straight to "On", skipping "Opening" and its input lock entirely.
        public void SetActivatedImmediate()
        {
            _state = SwitchState.On;
            ApplySprite(SwitchState.On);
            ApplyLight(SwitchState.On);
            ApplyBattery(SwitchState.On);
            // Explicit null check, not ?., per this project's Unity-object convention — ?. bypasses
            // Unity's overridden null check and an unassigned serialized reference can still throw.
            if (animator != null)
                animator.SetTrigger(OnTrigger);
        }

        private void ApplySprite(SwitchState state)
        {
            if (spriteRenderer == null) return;

            Sprite sprite = state switch
            {
                SwitchState.Closed => closedSprite,
                SwitchState.Opening => openingSprite,
                SwitchState.On => onSprite,
                _ => null
            };

            if (sprite != null)
                spriteRenderer.sprite = sprite;
        }

        // Light is off only while Closed — on for both Opening and On, per design.
        private void ApplyLight(SwitchState state)
        {
            if (lightObject != null)
                lightObject.SetActive(state != SwitchState.Closed);
        }

        private void ApplyBattery(SwitchState state)
        {
            if (batteryObject != null)
                batteryObject.SetActive(state == SwitchState.Opening);
        }

        // Fires once DoorCutawaySystem's camera cutaway for this switch's door has actually
        // finished (or immediately, if that door has no cutaway camera at all — see
        // DoorCutawaySystem.OnDoorOpened) — not the instant the switch itself reaches On.
        private void OnDoorCutawayFinished(DoorOpenedEvent e)
        {
            if (e.DoorId != doorId) return;
            ApplyBattery(SwitchState.On);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _interactionSystem?.Register(this);
            ShowPrompt();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _interactionSystem?.Unregister(this);
            HidePrompt();
        }

        private void OnDisable()
        {
            _interactionSystem?.Unregister(this);
            if (_doorEvents != null)
                _doorEvents.DoorCutawayFinished -= OnDoorCutawayFinished;
        }

        private void ShowPrompt()
        {
            if (interactPrompt == null || _state != SwitchState.Closed) return;
            if (_promptAnim != null) StopCoroutine(_promptAnim);
            _promptAnim = StartCoroutine(AnimatePrompt(true));
        }

        private void HidePrompt()
        {
            if (interactPrompt == null) return;
            if (_promptAnim != null) StopCoroutine(_promptAnim);
            _promptAnim = StartCoroutine(AnimatePrompt(false));
        }

        private IEnumerator AnimatePrompt(bool show)
        {
            if (show) interactPrompt.SetActive(true);
            var t = interactPrompt.transform;
            Vector3 from = show ? Vector3.zero : Vector3.one;
            Vector3 to = show ? Vector3.one : Vector3.zero;
            float elapsed = 0f;
            const float dur = 0.15f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(from, to, elapsed / dur);
                yield return null;
            }
            t.localScale = to;
            if (!show) interactPrompt.SetActive(false);
        }
    }
}
