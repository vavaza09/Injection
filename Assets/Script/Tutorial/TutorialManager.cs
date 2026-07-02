using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Game.Characters.Player;
using Game.Components.Skills;

namespace Game.Tutorial
{
    /// <summary>
    /// Drives the linear tutorial: turns on ability gating, then walks through the ordered list of
    /// steps. Each step's ability is unlocked when it begins; when the step reports Completed the
    /// manager shows a brief success beat then advances. When all steps are done it unlocks every
    /// ability and shows the completion message.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();
        [SerializeField] private TutorialPromptUI promptUI;
        [SerializeField] private float successFeedbackSeconds = 1f;
        [SerializeField] private string successMessage = "Nice!";
        [SerializeField] private string completeMessage = "Tutorial Complete!";

        [Header("Start Delay")]
        [SerializeField] private float startDelay = 0.6f;

        [Header("Completion Warp")]
        [SerializeField] private bool warpOnComplete = true;
        [SerializeField] private float completionDelay = 2f;
        [SerializeField] private string nextRoomId = "room_a";
        [SerializeField] private string nextSpawnPointId = "default";

        [Header("Dev")]
        [SerializeField] private bool enableDevSkip = true;
        [SerializeField] private Key devSkipKey = Key.RightBracket;

        private Player _player;
        private TutorialContext _context;
        private int _index = -1;
        private TutorialStep _current;
        private bool _running;

        [Inject]
        public void Construct(Player player, PlayerInputHandler input, IPlayerSkillEvents skillEvents, IEnergyStore energyStore)
        {
            _player = player;
            _context = new TutorialContext
            {
                Player = player,
                Input = input,
                SkillEvents = skillEvents,
                EnergyStore = energyStore
            };
        }

        private void Start()
        {
            if (promptUI == null) promptUI = FindAnyObjectByType<TutorialPromptUI>();

            _player?.SetAbilityGating(true);
            _running = true;
            StartCoroutine(DelayedStart());
        }

        private IEnumerator DelayedStart()
        {
            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);
            Advance();
        }

        private void Update()
        {
            if (!_running || !enableDevSkip || _current == null) return;
            if (Keyboard.current != null && Keyboard.current[devSkipKey].wasPressedThisFrame)
                SkipCurrent();
        }

        private void Advance()
        {
            _index++;
            if (_index >= steps.Count)
            {
                Finish();
                return;
            }

            _current = steps[_index];
            if (_current == null)
            {
                Advance();
                return;
            }

            if (!string.IsNullOrEmpty(_current.AbilityToUnlock))
                _player?.UnlockAbility(_current.AbilityToUnlock);

            _current.Completed += OnCurrentCompleted;
            promptUI?.Show(_current.Description, _current.PromptKeys);
            _current.Begin(_context);
        }

        private void OnCurrentCompleted()
        {
            if (_current != null) _current.Completed -= OnCurrentCompleted;
            StartCoroutine(SuccessThenAdvance());
        }

        private void SkipCurrent()
        {
            if (_current == null) return;
            _current.Completed -= OnCurrentCompleted;
            _current.Cleanup();
            StartCoroutine(SuccessThenAdvance());
        }

        private IEnumerator SuccessThenAdvance()
        {
            promptUI?.ShowSuccess(successMessage);
            yield return new WaitForSecondsRealtime(successFeedbackSeconds);
            Advance();
        }

        private void Finish()
        {
            _running = false;
            _current = null;
            _player?.UnlockAllAbilities();
            promptUI?.ShowSuccess(completeMessage);

            if (warpOnComplete)
                StartCoroutine(WarpAfterDelay());
        }

        private IEnumerator WarpAfterDelay()
        {
            yield return new WaitForSecondsRealtime(completionDelay);

            // RoomManager is DontDestroyOnLoad when running via Bootstrap.
            // In standalone DevScene it won't exist — FindAnyObjectByType returns null and we skip.
            var roomManager = FindAnyObjectByType<Game.Rooms.RoomManager>();
            roomManager?.LoadRoom(nextRoomId, nextSpawnPointId);
        }
    }
}
