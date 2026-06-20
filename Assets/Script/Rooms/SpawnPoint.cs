using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// Marks a named arrival location inside a room scene. The player is placed here
    /// when a portal / save / continue references this id. This is the single id+transform
    /// source — <see cref="SavePointTrigger"/> references one of these for its checkpoint.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnPointId;

        public string Id => spawnPointId;
        public Vector3 Position => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
            Gizmos.DrawSphere(transform.position, 0.35f);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
}
