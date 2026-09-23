using UnityEngine;
using Unity.Cinemachine;
using VContainer;
using Game.Components.Movement;

namespace Game.Rooms.Objectives
{
    /// <summary>
    /// Registers a door with DoorObjectiveSystem and forwards open/close calls to the existing
    /// DoorController (kept as a plain, decoupled visual receiver rather than merged in, so it stays
    /// reusable by any other trigger). Also carries the per-door cutaway camera/timings a designer
    /// authors by hand in-scene, read by DoorCutawaySystem once this door's DoorOpenedEvent fires.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorView : MonoBehaviour
    {
        [SerializeField] private string doorId;
        [SerializeField] private DoorController doorController;

        [Header("Cutaway")]
        [SerializeField] private CinemachineCamera cutsceneCamera;
        [SerializeField] private float cutsceneHoldDuration = 1.5f;
        [SerializeField] private int cutsceneRestingPriority = 0;

        private DoorObjectiveSystem _system;

        public string DoorId => doorId;
        public CinemachineCamera CutsceneCamera => cutsceneCamera;
        public float CutsceneHoldDuration => cutsceneHoldDuration;
        public int CutsceneRestingPriority => cutsceneRestingPriority;

        [Inject]
        public void Construct(DoorObjectiveSystem system)
        {
            _system = system;
        }

        private void Start()
        {
            _system?.RegisterDoor(doorId, this);
        }

        // Called by DoorObjectiveSystem on a live switch activation.
        public void Open()
        {
            // Explicit null check, not ?. — see DoorSwitchView.SetActivatedImmediate for why.
            if (doorController != null)
                doorController.Open();
        }

        // Called by DoorObjectiveSystem when this door was already persisted open on room load —
        // no cutscene, snap straight to the open visual state.
        public void SnapOpenImmediate()
        {
            if (doorController != null)
                doorController.Open();
        }
    }
}
