using System.Collections.Generic;
using VContainer;
using Core.Logging;
using Game.Persistence;

namespace Game.Rooms.Objectives
{
    /// <summary>
    /// Owns door open/closed state for the room. Doors persist via the existing generic
    /// SaveService.IsObjectiveComplete/MarkObjectiveComplete API (SaveData.objectivesCompleted),
    /// keyed by doorId — no SaveData schema change needed. Scoped per room (SceneLifetimeScope).
    /// </summary>
    public sealed class DoorObjectiveSystem
    {
        private sealed class DoorEntry
        {
            public DoorView View;
            public bool IsOpen;
        }

        private readonly Dictionary<string, DoorEntry> _doors = new Dictionary<string, DoorEntry>();
        private readonly SaveService _saveService;
        private readonly DoorObjectiveEvents _events;
        private readonly Core.Logging.ILogger _logger;

        [Inject]
        public DoorObjectiveSystem(SaveService saveService, DoorObjectiveEvents events, LoggerFactory loggerFactory)
        {
            _saveService = saveService;
            _events = events;
            _logger = loggerFactory?.CreateLogger("DoorObjectiveSystem");
        }

        // Called by DoorView.Start(). Catches up to a previously-persisted open state with no
        // cutscene — the cutaway only plays for a live activation, not a fresh room load.
        public void RegisterDoor(string doorId, DoorView view)
        {
            if (string.IsNullOrEmpty(doorId) || view == null) return;

            bool alreadyOpen = _saveService != null && _saveService.IsObjectiveComplete(doorId);
            _doors[doorId] = new DoorEntry { View = view, IsOpen = alreadyOpen };

            if (alreadyOpen)
                view.SnapOpenImmediate();
        }

        // Called by DoorSwitchView.Interact(). Idempotent — a repeat press on an already-open
        // door's switch is a no-op, not a replayed cutscene.
        public void ActivateSwitch(string switchId, string doorId)
        {
            if (string.IsNullOrEmpty(doorId)) return;
            if (!_doors.TryGetValue(doorId, out var entry))
            {
                _logger?.LogWarning($"[DoorObjectiveSystem] Switch '{switchId}' targets unknown door '{doorId}'.");
                return;
            }
            if (entry.IsOpen) return;

            entry.IsOpen = true;
            // Not entry.View.Open() here — DoorCutawaySystem calls that itself, timed to start only
            // once its camera fade-in actually reveals the door (or immediately, if this door has no
            // cutaway camera at all). Opening it here instead would start the door's own animation at
            // the exact same moment the cutaway's fade-out begins, so by the time the screen fades
            // back in the door would already be mid-way through or fully open off-screen.
            _saveService?.MarkObjectiveComplete(doorId);
            _events.RaiseDoorOpened(new DoorOpenedEvent(doorId));
        }

        // Used by DoorSwitchView.Start() to snap its own visual state on a save-restored room,
        // independent of DoorView's own Start() registration order.
        public bool IsDoorPersistedOpen(string doorId)
        {
            return !string.IsNullOrEmpty(doorId) && _saveService != null && _saveService.IsObjectiveComplete(doorId);
        }

        // Used by DoorCutawaySystem to resolve the per-door cutscene camera/timings authored on
        // the DoorView, once it knows which door just opened.
        public bool TryGetView(string doorId, out DoorView view)
        {
            if (!string.IsNullOrEmpty(doorId) && _doors.TryGetValue(doorId, out var entry))
            {
                view = entry.View;
                return true;
            }
            view = null;
            return false;
        }
    }
}
