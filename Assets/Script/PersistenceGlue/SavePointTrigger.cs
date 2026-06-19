using UnityEngine;
using VContainer;
using Game.Persistence;
using Game.Components.Skills;
using Game.Rooms;

/// <summary>
/// Save point: writes the death-respawn checkpoint (room + spawn point + energy) and
/// heals the player. The arrival location is a composed <see cref="SpawnPoint"/> so the
/// save id and the place the player re-appears share a single source of truth.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SavePointTrigger : MonoBehaviour
{
    [SerializeField] private SpawnPoint spawnPoint;

    public string Id => spawnPoint != null ? spawnPoint.Id : null;

    private SaveService _saveService;
    private IEnergyStore _energy;
    private IRoomLoader _roomLoader;

    [Inject]
    public void Construct(SaveService saveService, IEnergyStore energy, IRoomLoader roomLoader)
    {
        _saveService = saveService;
        _energy = energy;
        _roomLoader = roomLoader;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_saveService == null) return;
        if (spawnPoint == null)
        {
            Debug.LogWarning("[SavePointTrigger] No SpawnPoint assigned — cannot save.", this);
            return;
        }

        var player = other.GetComponent<Player>();
        if (player == null) return;

        var data = _saveService.Load() ?? new SaveData();
        data.checkpoint.roomId = _roomLoader?.CurrentRoomId;
        data.checkpoint.spawnPointId = spawnPoint.Id;
        data.checkpoint.energy = _energy?.Current ?? 0;
        _saveService.Save(data);

        player.HealToFull();
    }

    private void OnDrawGizmos()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.Position : transform.position;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(pos, 0.4f);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(pos, 0.4f);
    }
}
