using UnityEngine;

namespace Game.Components.Movement
{
    /// <summary>
    /// Platform that swings around a pivot (rotate + translate together) and rattles
    /// harder over time, as if it's about to tear loose from its mount.
    /// Pivot is a fixed world point offset from the platform's rest position.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SwingingPlatform : MonoBehaviour
    {
        [Header("Pivot")]
        [Tooltip("Hinge point relative to the platform's rest position. e.g. (0, 3) = hangs from 3 units above.")]
        [SerializeField] private Vector2 pivotOffset = new Vector2(0f, 3f);

        [Header("Swing")]
        [SerializeField] private float swingAngle = 22f;      // max degrees off rest
        [SerializeField] private float swingSpeed = 2f;       // radians/sec of the oscillation
        [Tooltip("Per-second decay of swing amplitude. 0 = swings forever.")]
        [SerializeField] private float damping = 0f;
        [SerializeField] private bool swingOnStart = true;

        [Header("Coming Loose (rattle)")]
        [Tooltip("Extra positional jitter once fully built up.")]
        [SerializeField] private float shakeAmplitude = 0.06f;
        [Tooltip("Extra random tilt in degrees once fully built up.")]
        [SerializeField] private float shakeAngle = 3f;
        [SerializeField] private float shakeSpeed = 2f;
        [Tooltip("Seconds for the rattle to ramp from 0 to full after a swing starts.")]
        [SerializeField] private float buildUpTime = 3f;

        private Rigidbody2D _rb;
        private Vector2 _pivot;
        private Vector2 _restArm;      // pivot -> platform vector at rest
        private float _restRotation;
        private float _phase;
        private float _elapsed;
        private bool _swinging;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            Vector2 rest = _rb.position;
            _pivot = rest + pivotOffset;
            _restArm = rest - _pivot;
            _restRotation = _rb.rotation;
        }

        private void Start()
        {
            if (swingOnStart) TriggerSwing();
        }

        /// <summary>Kick off (or restart) the swing from full intensity.</summary>
        public void TriggerSwing()
        {
            _swinging = true;
            _phase = 0f;
            _elapsed = 0f;
        }

        public void StopSwing() => _swinging = false;

        private void FixedUpdate()
        {
            if (!_swinging) return;

            float dt = Time.fixedDeltaTime;
            _elapsed += dt;
            _phase += swingSpeed * dt;

            float amplitude = damping > 0f ? swingAngle * Mathf.Exp(-damping * _elapsed) : swingAngle;
            float angle = amplitude * Mathf.Sin(_phase);

            float build = buildUpTime > 0f ? Mathf.Clamp01(_elapsed / buildUpTime) : 1f;
            float n1 = Mathf.PerlinNoise(_elapsed * shakeSpeed, 0f) - 0.5f;
            float n2 = Mathf.PerlinNoise(0f, _elapsed * shakeSpeed) - 0.5f;
            float n3 = Mathf.PerlinNoise(_elapsed * shakeSpeed, _elapsed * shakeSpeed) - 0.5f;

            Vector2 shake = new Vector2(n1, n2) * (2f * shakeAmplitude * build);
            float tilt = n3 * (2f * shakeAngle * build);

            Vector2 arm = Quaternion.Euler(0f, 0f, angle) * _restArm;
            _rb.MovePosition(_pivot + arm + shake);
            _rb.MoveRotation(_restRotation + angle + tilt);
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 pivot = Application.isPlaying ? _pivot : (Vector2)transform.position + pivotOffset;
            Vector2 restArm = Application.isPlaying ? _restArm : (Vector2)transform.position - pivot;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pivot, 0.15f);
            Gizmos.DrawLine(pivot, pivot + restArm);

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 end = (Vector2)(Quaternion.Euler(0f, 0f, swingAngle * i) * restArm) + pivot;
                Gizmos.DrawLine(pivot, end);
            }
        }
    }
}
