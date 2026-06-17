using UnityEngine;

namespace Game.Components.Cloth
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class CapeAnchorFollower : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private Rigidbody2D _rb;
        private Vector3 _localOffset;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (target != null)
                _localOffset = transform.position - target.position;
        }

        private void FixedUpdate()
        {
            if (target == null) return;
            _rb.MovePosition((Vector2)(target.position + _localOffset));
        }
    }
}
