using UnityEngine;

namespace Game.Spawning
{
    /// <summary>
    /// Declarative, data-only marker placed in a room scene. Describes WHICH enemy
    /// spawns WHERE. <see cref="RoomSpawner"/> reads these on room load and instantiates
    /// them fresh every entry — there is no per-enemy save state by design.
    /// </summary>
    public class EnemySpawnMarker : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [Tooltip("Optional override; defaults to this marker's own transform.")]
        [SerializeField] private Transform spawnAt;
        [SerializeField] private bool faceLeft;

        public GameObject EnemyPrefab => enemyPrefab;
        public Vector3 Position => spawnAt != null ? spawnAt.position : transform.position;
        public Quaternion Rotation => spawnAt != null ? spawnAt.rotation : transform.rotation;
        public bool FaceLeft => faceLeft;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.55f);
            Gizmos.DrawWireCube(Position, Vector3.one * 0.6f);
            Gizmos.DrawLine(Position, Position + (FaceLeft ? Vector3.left : Vector3.right));
        }
    }
}
