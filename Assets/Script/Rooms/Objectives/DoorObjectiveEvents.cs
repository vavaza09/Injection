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
    }

    // Concrete implementation — only DoorObjectiveSystem raises events, and only on a live
    // switch activation (never on the save-restore catch-up path), so DoorCutawaySystem never
    // replays the cutscene for a door that was already open when the room loaded.
    public sealed class DoorObjectiveEvents : IDoorObjectiveEvents
    {
        public event Action<DoorOpenedEvent> DoorOpened;

        internal void RaiseDoorOpened(DoorOpenedEvent e) => DoorOpened?.Invoke(e);
    }
}
