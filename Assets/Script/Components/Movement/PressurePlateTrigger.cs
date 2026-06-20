using UnityEngine;
using UnityEngine.Events;

namespace Game.Components.Movement
{
    [RequireComponent(typeof(Collider2D))]
    public class PressurePlateTrigger : MonoBehaviour
    {
        [SerializeField] private MovingPlatform platform;
        [SerializeField] private UnityEvent onPressed;

        private bool _playerInside;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_playerInside) return;
            if (!other.CompareTag("Player")) return;

            _playerInside = true;
            platform?.Activate();
            onPressed?.Invoke();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;
        }
    }
}
