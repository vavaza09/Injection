using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using Core.Logging;
using Game.Pause;
using Game.UI;

namespace Game.Rooms.Objectives
{
    /// <summary>
    /// Subscribes to DoorObjectiveEvents and plays the fade-to-black / cut-to-door / hold /
    /// fade-to-black / cut-back sequence: freezes the player (PauseStack + PlayerInputGate, same
    /// idiom as ComicPlayer) while ScreenFader hides the hard Cinemachine priority swap to/from the
    /// door's own scene-authored cutscene camera. Scoped per room; disposed with the scope.
    /// </summary>
    public sealed class DoorCutawaySystem : IDisposable
    {
        // Above the room vcams' own active/inactive convention (20/10, see CameraZoneTrigger) so the
        // cutscene camera always wins the blend while raised, regardless of scene tuning.
        private const int CutawayPriority = 30;

        private readonly DoorObjectiveSystem _doorSystem;
        // Concrete type, not IDoorObjectiveEvents — this system is the one that RAISES
        // DoorCutawayFinished (DoorObjectiveSystem is the only other raiser, for DoorOpened),
        // and RaiseDoorCutawayFinished is internal, not part of the interface.
        private readonly DoorObjectiveEvents _events;
        private readonly Core.Logging.ILogger _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        [Inject]
        public DoorCutawaySystem(DoorObjectiveSystem doorSystem, DoorObjectiveEvents events, LoggerFactory loggerFactory)
        {
            _doorSystem = doorSystem;
            _events = events;
            _logger = loggerFactory?.CreateLogger("DoorCutawaySystem");
            _events.DoorOpened += OnDoorOpened;
        }

        public void Dispose()
        {
            _events.DoorOpened -= OnDoorOpened;
            _cts.Cancel();
            _cts.Dispose();
        }

        private void OnDoorOpened(DoorOpenedEvent e)
        {
            if (!_doorSystem.TryGetView(e.DoorId, out var view) || view.CutsceneCamera == null)
            {
                _logger?.LogWarning($"[DoorCutawaySystem] Door '{e.DoorId}' has no cutscene camera assigned — skipping cutaway.");
                // No cutaway to wait for — fire the "finished" signal immediately so anything
                // gated on it (e.g. DoorSwitchView's battery indicator) doesn't hang forever.
                _events.RaiseDoorCutawayFinished(e);
                return;
            }

            PlayCutawayAsync(view, e, _cts.Token).Forget();
        }

        private async UniTaskVoid PlayCutawayAsync(DoorView view, DoorOpenedEvent e, CancellationToken token)
        {
            var camera = view.CutsceneCamera;
            int restingPriority = view.CutsceneRestingPriority;

            // The room's CinemachineBrain usually has a non-zero DefaultBlend (smooth ease between
            // vcams) for normal gameplay transitions (e.g. CameraZoneTrigger). That blend would eat
            // into or outlast our fade/hold window and make the priority swap look like nothing
            // happened, since the swap itself is meant to be a hard cut hidden behind the fade — not
            // a second, competing transition. Force Cut for just this cutaway's two swaps, then put
            // the room's own blend setting back exactly as found, so unrelated camera transitions
            // elsewhere in the level are untouched.
            var brain = UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>();
            CinemachineBlendDefinition originalBlend = default;
            bool blendOverridden = false;
            if (brain != null)
            {
                originalBlend = brain.DefaultBlend;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                blendOverridden = true;
            }

            PauseStack.Instance.Push("doorCutaway");
            PlayerInputGate.Set(false);

            try
            {
                await FadeOutAsync(token);
                camera.Priority = CutawayPriority;
                await FadeInAsync(token);

                await UniTask.Delay(TimeSpan.FromSeconds(view.CutsceneHoldDuration), ignoreTimeScale: true, cancellationToken: token);

                await FadeOutAsync(token);
                camera.Priority = restingPriority;
                await FadeInAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Scope torn down (room reload/death) mid-cutaway — leave the camera/fade state as-is,
                // the reload replaces the whole scene anyway.
            }
            finally
            {
                PlayerInputGate.Set(true);
                PauseStack.Instance.Release("doorCutaway");

                if (blendOverridden)
                    brain.DefaultBlend = originalBlend;

                // Fire on cancellation too (not just normal completion) — anything waiting on
                // this (e.g. DoorSwitchView's battery indicator) must not hang forever just
                // because the scope tore down mid-cutaway.
                _events.RaiseDoorCutawayFinished(e);
            }
        }

        private static UniTask FadeOutAsync(CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource();
            token.Register(() => tcs.TrySetCanceled());
            ScreenFader.Instance.FadeOut(() => tcs.TrySetResult());
            return tcs.Task;
        }

        private static UniTask FadeInAsync(CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource();
            token.Register(() => tcs.TrySetCanceled());
            ScreenFader.Instance.FadeIn(() => tcs.TrySetResult());
            return tcs.Task;
        }
    }
}
