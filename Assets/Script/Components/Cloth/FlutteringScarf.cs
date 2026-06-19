using UnityEngine;
using Game.Components.Movement;

namespace Game.Components.Cloth
{
    /// <summary>
    /// A long rectangular scarf that flutters continuously as if blown by wind — even while idle.
    /// Simulates an internal chain of Verlet nodes (no SpriteSkin / child bones) and rebuilds a
    /// procedural quad-strip mesh from those nodes every frame.
    ///
    /// Three layered motion sources:
    ///   1. Rest pose      — streams horizontally BEHIND the facing direction (constant, persists at idle).
    ///   2. Perlin wind    — per-node phase offset produces a traveling ripple (the idle flutter).
    ///   3. Velocity trail — player movement drags the scarf opposite to motion (layers on top).
    ///
    /// Built around the same Verlet math as <see cref="SpringBoneChain"/> but feeds a generated mesh
    /// instead of skinned bones. Keep this GameObject at the scene ROOT (identity transform) so the
    /// world-space simulation isn't double-flipped by the player's localScale flip — see plan notes.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FlutteringScarf : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Neck point the scarf root follows (a transform on the player).")]
        [SerializeField] private Transform anchor;
        [Tooltip("The flipping transform — facing is read from Mathf.Sign(localScale.x).")]
        [SerializeField] private Transform characterTransform;
        [Tooltip("Optional: drives the velocity-trailing layer via GetVelocity().")]
        [SerializeField] private MovementComponent movementComponent;

        [Header("Chain")]
        [SerializeField, Min(2)] private int segmentCount = 14;
        [SerializeField, Min(0.01f)] private float totalLength = 2.0f;
        [SerializeField, Min(0.01f)] private float width = 0.25f;

        [Header("Verlet")]
        [Tooltip("Higher = the chain holds momentum longer and swings more freely with character motion (less stiff). Too high never settles at idle.")]
        [SerializeField, Range(0.8f, 0.995f)] private float damping = 0.92f;
        [Tooltip("Gentle smoothing toward a straight continuation of the parent segment. Higher relaxes kinks / tail flailing.")]
        [SerializeField, Range(0f, 0.5f)] private float bendSmoothing = 0.1f;
        [SerializeField] private float gravityScale = 0.4f;
        [SerializeField, Range(1, 8)] private int constraintIterations = 4;

        [Header("Wind")]
        [Tooltip("Constant wind acceleration blowing the scarf in the stream direction (behind + down). Higher = streams more taut/horizontal.")]
        [SerializeField] private float windStrength = 6f;
        [Tooltip("Oscillating flutter acceleration (perpendicular to the stream), grows toward the tail. This is the idle 'sway'. Lower = calmer idle.")]
        [SerializeField] private float flutterStrength = 4f;
        [Tooltip("How fast the flutter wave animates over time.")]
        [SerializeField] private float noiseSpeed = 1.2f;
        [Tooltip("Phase shift per node — makes the ripple travel down the scarf. Keep small for one smooth long wave; high values cause kinky tail thrashing.")]
        [SerializeField] private float perBonePhaseOffset = 0.2f;

        [Header("Stream direction / facing")]
        [Tooltip("Horizontal component of the wind — streams behind the facing direction.")]
        [SerializeField] private float restDirectionBias = 1.0f;
        [Tooltip("Downward component of the wind. Higher = the stream angles further down behind the player.")]
        [SerializeField] private float restSag = 0.6f;

        [Header("Velocity trailing")]
        [Tooltip("How strongly the character's horizontal speed drags the scarf opposite to motion.")]
        [SerializeField] private float velocityInfluence = 0.6f;

        [Header("Rendering")]
        [SerializeField] private Material material;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = -1;

        // Simulation state
        private Vector3[] _pos;
        private Vector3[] _prevPos;
        private float _segLen;
        private int _facing = 1;

        // Mesh state
        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int[] _triangles;
        private int _builtSegmentCount = -1;

        private void Awake()
        {
            _mesh = new Mesh { name = "ScarfRibbon" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _meshRenderer = GetComponent<MeshRenderer>();
            if (material != null)
                _meshRenderer.sharedMaterial = material;
            _meshRenderer.sortingLayerName = sortingLayerName;
            _meshRenderer.sortingOrder = sortingOrder;
        }

        private void Start() => Initialize();
        private void OnEnable() => Initialize();

        private void Initialize()
        {
            if (anchor == null) return;
            if (segmentCount < 2) segmentCount = 2;

            int nodeCount = segmentCount + 1;
            _segLen = totalLength / segmentCount;

            _pos = new Vector3[nodeCount];
            _prevPos = new Vector3[nodeCount];

            _facing = ReadFacing();
            Vector3 restStep = RestStep();

            // Seed nodes laid out along the rest direction (not piled at one point) to avoid
            // a degenerate first-frame mesh / NaN tangents.
            _pos[0] = anchor.position;
            for (int i = 1; i < nodeCount; i++)
                _pos[i] = _pos[i - 1] + restStep;
            for (int i = 0; i < nodeCount; i++)
                _prevPos[i] = _pos[i];

            AllocateMeshBuffers(nodeCount);
            BuildMesh();
        }

        private void AllocateMeshBuffers(int nodeCount)
        {
            _vertices = new Vector3[nodeCount * 2];
            _normals = new Vector3[nodeCount * 2];
            _uvs = new Vector2[nodeCount * 2];

            // Triangle indices are static for a given segment count — build once.
            _triangles = new int[segmentCount * 6];
            for (int i = 0; i < segmentCount; i++)
            {
                int v = i * 2;       // verts: v(L), v+1(R), v+2(nextL), v+3(nextR)
                int t = i * 6;
                // Two triangles per quad, wound to face the camera (-Z). Reverse or use Cull Off if invisible.
                _triangles[t + 0] = v;
                _triangles[t + 1] = v + 2;
                _triangles[t + 2] = v + 1;
                _triangles[t + 3] = v + 1;
                _triangles[t + 4] = v + 2;
                _triangles[t + 5] = v + 3;
            }
            _builtSegmentCount = segmentCount;
        }

        private void LateUpdate()
        {
            if (anchor == null || _pos == null || _pos.Length < 2) return;

            _facing = ReadFacing();
            // Stream direction = where the wind blows the scarf (behind facing + downward).
            Vector2 stream = RestDir();
            Vector3 streamDir = stream;
            // Perpendicular to the stream — the flutter oscillation pushes nodes along this axis.
            Vector3 perpDir = new Vector3(-stream.y, stream.x, 0f);

            float dt = Time.deltaTime;
            float dt2 = dt * dt;

            // Forces are accelerations (units/s^2). Verlet integrates them as a*dt^2; momentum accumulates
            // via the (cur - prev) velocity term so the scarf swings and lags with character motion.
            Vector3 gravity = (Vector3)Physics2D.gravity * gravityScale;
            Vector3 windAccel = streamDir * windStrength;
            float vx = movementComponent != null ? movementComponent.GetVelocity().x : 0f;
            Vector3 velAccel = new Vector3(-vx * velocityInfluence, 0f, 0f);

            float t = Time.time * noiseSpeed;
            int last = _pos.Length - 1;

            // Root is pinned to the anchor — all character motion enters here and propagates as inertia.
            _pos[0] = anchor.position;
            _prevPos[0] = _pos[0];

            for (int i = 1; i < _pos.Length; i++)
            {
                Vector3 cur = _pos[i];
                Vector3 inertia = (cur - _prevPos[i]) * damping;

                // Oscillating flutter, perpendicular to the stream, growing toward the free end.
                float fraction = (float)i / last;
                float n = Mathf.PerlinNoise(t + i * perBonePhaseOffset, i * 0.13f) * 2f - 1f;
                Vector3 flutter = perpDir * (n * flutterStrength * fraction);

                Vector3 accel = windAccel + gravity + velAccel + flutter;
                Vector3 next = cur + inertia + accel * dt2;

                // Light bending smoothing: nudge toward a straight continuation of the parent segment so
                // sharp kinks relax, but keep it weak so the cloth stays floppy and free-swinging.
                if (i >= 2 && bendSmoothing > 0f)
                {
                    Vector3 segPrev = _pos[i - 1] - _pos[i - 2];
                    if (segPrev.sqrMagnitude > 0.000001f)
                    {
                        Vector3 straight = _pos[i - 1] + segPrev.normalized * _segLen;
                        next = Vector3.Lerp(next, straight, bendSmoothing);
                    }
                }

                _prevPos[i] = cur;
                _pos[i] = next;
            }

            // Distance constraint — enforce segment length (keeps it from stretching; root stays pinned).
            for (int it = 0; it < constraintIterations; it++)
            {
                for (int i = 1; i < _pos.Length; i++)
                {
                    Vector3 diff = _pos[i] - _pos[i - 1];
                    float mag = diff.magnitude;
                    Vector3 dir = mag > 0.0001f ? diff / mag : streamDir;
                    _pos[i] = _pos[i - 1] + dir * _segLen;
                }
            }

            BuildMesh();
        }

        private void BuildMesh()
        {
            if (_pos == null || _pos.Length < 2) return;

            if (segmentCount != _builtSegmentCount || _vertices == null)
                AllocateMeshBuffers(_pos.Length);

            int n = _pos.Length;
            float halfW = width * 0.5f;
            Vector3 lastPerp = (Vector3)RestDir(); // fallback for degenerate tangents
            // initial fallback perpendicular
            lastPerp = new Vector3(-lastPerp.y, lastPerp.x, 0f);

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent;
                if (i == 0) tangent = _pos[1] - _pos[0];
                else if (i == n - 1) tangent = _pos[n - 1] - _pos[n - 2];
                else tangent = _pos[i + 1] - _pos[i - 1];

                Vector3 perp;
                if (tangent.sqrMagnitude > 0.000001f)
                {
                    Vector3 tn = tangent.normalized;
                    perp = new Vector3(-tn.y, tn.x, 0f);
                    lastPerp = perp;
                }
                else
                {
                    perp = lastPerp;
                }

                int v = i * 2;
                _vertices[v] = transform.InverseTransformPoint(_pos[i] + perp * halfW);
                _vertices[v + 1] = transform.InverseTransformPoint(_pos[i] - perp * halfW);

                _normals[v] = Vector3.back;
                _normals[v + 1] = Vector3.back;

                float u = (float)i / (n - 1);
                _uvs[v] = new Vector2(u, 0f);
                _uvs[v + 1] = new Vector2(u, 1f);
            }

            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();
        }

        private int ReadFacing()
        {
            if (characterTransform == null) return _facing;
            float sx = characterTransform.localScale.x;
            if (sx > 0f) return 1;
            if (sx < 0f) return -1;
            return _facing; // facing == 0 → keep previous
        }

        /// <summary>Unit rest direction: streams behind facing, mixed with downward sag.</summary>
        private Vector2 RestDir()
        {
            Vector2 dir = new Vector2(-_facing * restDirectionBias, -restSag);
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
        }

        private Vector3 RestStep() => (Vector3)(RestDir() * _segLen);

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }

        private void OnValidate()
        {
            if (segmentCount < 2) segmentCount = 2;
            if (totalLength < 0.01f) totalLength = 0.01f;
            if (width < 0.01f) width = 0.01f;
        }
    }
}
