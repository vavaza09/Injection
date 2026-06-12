using UnityEngine;

namespace Game.Components.Movement
{
    public class SwingPoint : MonoBehaviour
    {
        [SerializeField] private Vector2 hangOffset = new Vector2(0f, -0.3f);

        public Vector2 AnchorPosition => (Vector2)transform.position + hangOffset;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawLine(transform.position, (Vector3)AnchorPosition);
        }
    }
}
