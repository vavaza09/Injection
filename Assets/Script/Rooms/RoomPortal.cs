using UnityEngine;
using VContainer;

namespace Game.Rooms
{
    /// <summary>
    /// A trigger that sends the player to another room. Injected per-scene by the
    /// room's child LifetimeScope. Stateless re: save — it only requests a transition.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomPortal : MonoBehaviour
    {
        [SerializeField] private string targetRoomId;
        [SerializeField] private string targetSpawnPointId;

        private IRoomLoader _roomLoader;

        [Inject]
        public void Construct(IRoomLoader roomLoader)
        {
            _roomLoader = roomLoader;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_roomLoader == null || _roomLoader.IsTransitioning) return;

            _roomLoader.LoadRoom(targetRoomId, targetSpawnPointId);
        }
    }
}
