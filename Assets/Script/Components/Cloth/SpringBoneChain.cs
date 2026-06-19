using UnityEngine;
using Game.Components.Movement;

namespace Game.Components.Cloth
{
    public class SpringBoneChain : MonoBehaviour
    {
        [Header("Bone Chain")]
        [SerializeField] private Transform[] bones;

        [Header("Spring Physics")]
        [SerializeField, Range(0f, 1f)] private float stiffness = 0.08f;
        [SerializeField, Range(0f, 1f)] private float damping = 0.88f;
        [SerializeField] private float gravityScale = 0.5f;

        [Header("Player Velocity Influence")]
        [SerializeField] private float velocityInfluence = 0.04f;
        [SerializeField] private MovementComponent movementComponent;

        private Vector3[] _positions;
        private Vector3[] _prevPositions;
        private float[] _boneLengths;

        private void Start() => Initialize();
        private void OnEnable() => Initialize();

        private void Initialize()
        {
            if (bones == null || bones.Length == 0) return;

            _positions = new Vector3[bones.Length];
            _prevPositions = new Vector3[bones.Length];
            _boneLengths = new float[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                _positions[i] = bones[i].position;
                _prevPositions[i] = bones[i].position;
            }

            // Compute natural rest length from initial bone placement
            for (int i = 1; i < bones.Length; i++)
            {
                _boneLengths[i] = Vector3.Distance(bones[i].position, bones[i - 1].position);
                if (_boneLengths[i] < 0.001f)
                    _boneLengths[i] = 0.15f;
            }
        }

        private void LateUpdate()
        {
            if (bones == null || bones.Length < 2 || _positions == null || bones[0] == null) return;

            // Root always follows its Transform (driven by ScarfAnchor)
            _positions[0] = bones[0].position;
            _prevPositions[0] = _positions[0];

            Vector2 playerVelocity = movementComponent != null
                ? movementComponent.GetVelocity()
                : Vector2.zero;

            float dt = Time.deltaTime;
            Vector3 gravityForce = (Vector3)Physics2D.gravity * gravityScale * dt * dt;
            // Cloth trails opposite to movement direction
            Vector3 windForce = new Vector3(-playerVelocity.x * velocityInfluence, 0f, 0f) * dt * dt;

            for (int i = 1; i < bones.Length; i++)
            {
                Vector3 cur = _positions[i];
                Vector3 prev = _prevPositions[i];

                Vector3 velocity = (cur - prev) * damping;
                Vector3 next = cur + velocity + gravityForce + windForce;

                // Stiffness: blend toward natural hang (straight below parent)
                Vector3 restPos = _positions[i - 1] + Vector3.down * _boneLengths[i];
                next = Vector3.Lerp(next, restPos, stiffness);

                // Distance constraint: enforce bone length
                Vector3 diff = next - _positions[i - 1];
                next = _positions[i - 1] + diff.normalized * _boneLengths[i];

                _prevPositions[i] = cur;
                _positions[i] = next;
                bones[i].position = next;
            }

            UpdateRotations();

            // Parent rotation shifts children's world positions — re-apply to correct cascade
            for (int i = 1; i < bones.Length; i++)
            {
                if (bones[i] != null)
                    bones[i].position = _positions[i];
            }
        }

        private void UpdateRotations()
        {
            for (int i = 0; i < bones.Length - 1; i++)
            {
                Vector3 dir = _positions[i + 1] - _positions[i];
                if (dir.sqrMagnitude > 0.0001f)
                {
                    // Bones point along local Y axis (Unity 2D Skinning Editor convention)
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    bones[i].rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }
    }
}
