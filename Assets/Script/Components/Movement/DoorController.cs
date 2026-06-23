using UnityEngine;

namespace Game.Components.Movement
{
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private bool startClosed = true;

        private static readonly int OpenTrigger = Animator.StringToHash("Open");
        private static readonly int CloseTrigger = Animator.StringToHash("Close");

        private void Start()
        {
            if (startClosed) ApplyClosed();
        }

        public void Open()
        {
            if (blockingCollider != null) blockingCollider.enabled = false;
            if (animator != null)
                animator.SetTrigger(OpenTrigger);
            else if (visualRoot != null)
                visualRoot.SetActive(false);
        }

        public void Close()
        {
            ApplyClosed();
            if (animator != null)
                animator.SetTrigger(CloseTrigger);
        }

        private void ApplyClosed()
        {
            if (blockingCollider != null) blockingCollider.enabled = true;
            if (animator == null && visualRoot != null) visualRoot.SetActive(true);
        }
    }
}
