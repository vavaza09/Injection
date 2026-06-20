using UnityEngine;
using UnityEngine.Events;

namespace Game.Components.Movement
{
    public enum MovementMode { Auto, EventTriggered }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private MovementMode mode = MovementMode.Auto;

        [Header("Path")]
        [SerializeField] private Vector2 moveOffset = new Vector2(5f, 0f);

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float pauseAtEnds = 0f;
        [SerializeField] private bool useEasing = false;

        [Header("Rider Carry")]
        [SerializeField] private LayerMask riderLayer;
        [SerializeField] private float riderCheckYOffset = 0.05f;
        [SerializeField] private float snapRiseThreshold = 0.1f;

        [Header("Events")]
        [SerializeField] private UnityEvent onReachedEndpoint;

        private enum PlatformState { Moving, Pausing, Idle }

        private Rigidbody2D _rb;
        private Collider2D _col;
        private Vector2 _pointA;
        private Vector2 _pointB;
        private float _t;
        private bool _targetIsB = true;
        private PlatformState _state;
        private float _pauseTimer;
        private float _offsetLength;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _col = GetComponent<Collider2D>();

            if (riderLayer.value == 0)
                riderLayer = LayerMask.GetMask("Player");

            _pointA = _rb.position;
            _pointB = _pointA + moveOffset;
            _offsetLength = moveOffset.magnitude;
            if (_offsetLength < 0.001f) _offsetLength = 0.001f;

            _t = 0f;
        }

        private void Start()
        {
            if (mode == MovementMode.Auto)
            {
                _targetIsB = true;
                _state = PlatformState.Moving;
            }
            else
            {
                _state = PlatformState.Idle;
            }
        }

        private void FixedUpdate()
        {
            if (_state == PlatformState.Idle) return;

            if (_state == PlatformState.Pausing)
            {
                _pauseTimer -= Time.fixedDeltaTime;
                if (_pauseTimer <= 0f)
                    _state = PlatformState.Moving;
                return;
            }

            float step = (moveSpeed / _offsetLength) * Time.fixedDeltaTime;
            float target = _targetIsB ? 1f : 0f;
            _t = Mathf.MoveTowards(_t, target, step);

            float easedT = useEasing ? Mathf.SmoothStep(0f, 1f, _t) : _t;
            Vector2 newPos = Vector2.Lerp(_pointA, _pointB, easedT);

            Vector2 platformDelta = newPos - _rb.position;
            _rb.MovePosition(newPos);

            CarryRiders(platformDelta, newPos);

            bool reachedTarget = Mathf.Approximately(_t, target);
            if (reachedTarget)
            {
                onReachedEndpoint?.Invoke();

                if (mode == MovementMode.Auto)
                {
                    _targetIsB = !_targetIsB;
                    if (pauseAtEnds > 0f)
                    {
                        _pauseTimer = pauseAtEnds;
                        _state = PlatformState.Pausing;
                    }
                }
                else
                {
                    _state = PlatformState.Idle;
                }
            }
        }

        public void Activate()
        {
            if (mode != MovementMode.EventTriggered) return;

            _targetIsB = !_targetIsB;
            _state = PlatformState.Moving;
        }

        private void CarryRiders(Vector2 platformDelta, Vector2 platformNewPos)
        {
            Bounds bounds = _col.bounds;
            // Use platform's new position to compute the up-to-date top surface
            float topY = platformNewPos.y + bounds.extents.y;

            Vector2 probeSize = new Vector2(bounds.size.x, riderCheckYOffset * 2f + 0.05f);
            Vector2 probeCenter = new Vector2(platformNewPos.x, topY + riderCheckYOffset);

            Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f, riderLayer);
            foreach (Collider2D hit in hits)
            {
                Rigidbody2D riderRb = hit.attachedRigidbody;
                if (riderRb == null || riderRb == _rb) continue;

                // Skip if player is currently dropping through this platform
                if (Physics2D.GetIgnoreCollision(_col, hit)) continue;

                // Skip if player's feet are still below platform surface (passing through from below)
                if (hit.bounds.min.y < topY) continue;

                // Skip if rider is moving upward
                if (riderRb.linearVelocity.y > snapRiseThreshold) continue;

                Vector2 p = riderRb.position;

                // Horizontal carry
                p.x += platformDelta.x;

                // Vertical snap: keep rider's foot exactly on the platform top surface
                float footOffset = riderRb.position.y - hit.bounds.min.y;
                p.y = topY + footOffset;

                riderRb.position = p;
            }
        }

        private void OnDrawGizmos()
        {
            Vector2 origin = Application.isPlaying
                ? _pointA
                : (Vector2)transform.position;
            Vector2 end = origin + moveOffset;

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawLine(origin, end);

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.4f);
            float w = Mathf.Abs(moveOffset.x) < 0.01f ? 2f : Mathf.Abs(moveOffset.x);
            Gizmos.DrawWireCube(origin, new Vector3(w, 0.5f, 0f));
            Gizmos.DrawWireSphere(end, 0.2f);
        }
    }
}
