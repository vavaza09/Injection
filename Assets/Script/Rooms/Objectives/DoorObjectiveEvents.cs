using System;

namespace Game.Rooms.Objectives
{
    public readonly struct DoorOpenedEvent
    {
        public readonly string DoorId;

        public DoorOpenedEvent(string doorId)
        {
            DoorId = doorId;
        }
    }

    public interface IDoorObjectiveEvents
    {
        event Action<DoorOpenedEvent> DoorOpened;

        // Raised by DoorCutawaySystem once its camera cutaway for this door has finished (or
        // immediately, if the door has no cutaway camera at all) — never on the save-restore
        // catch-up path, since DoorOpened itself never fires there either. Anything that timed
        // its own visual state off "the door is now open" but actually needs to wait for the
        // cutaway to finish showing it (e.g. DoorSwitchView's battery indicator) should use this
        // instead of DoorOpened.
        event Action<DoorOpenedEvent> DoorCutawayFinished;
    }

    // Concrete implementation — only DoorObjectiveSystem raises DoorOpened, and only on a live
    // switch activation (never on the save-restore catch-up path), so DoorCutawaySystem never
    // replays the cutscene for a door that was already open when the room loaded. Only
    // DoorCutawaySystem raises DoorCutawayFinished.
    public sealed class DoorObjectiveEvents : IDoorObjectiveEvents
    {
        public event Action<DoorOpenedEvent> DoorOpened;
        public event Action<DoorOpenedEvent> DoorCutawayFinished;

        internal void RaiseDoorOpened(DoorOpenedEvent e) => DoorOpened?.Invoke(e);
        internal void RaiseDoorCutawayFinished(DoorOpenedEvent e) => DoorCutawayFinished?.Invoke(e);
    }
}
