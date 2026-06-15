using UnityEngine;
using VContainer;
using Game.Persistence;
using Game.Components.Skills;

public class SavePointTrigger : MonoBehaviour
{
    [SerializeField] private string savePointId;
    [SerializeField] private Transform spawnPoint;

    public string Id => savePointId;
    public Transform SpawnTransform => spawnPoint != null ? spawnPoint : transform;

    private SaveService _saveService;
    private IEnergyStore _energy;

    [Inject]
    public void Construct(SaveService saveService, IEnergyStore energy)
    {
        _saveService = saveService;
        _energy = energy;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_saveService == null) return;

        var player = other.GetComponent<Player>();
        if (player == null) return;

        var data = _saveService.Load() ?? new SaveData();
        data.spawnPointId = savePointId;
        data.playerEnergy = _energy?.Current ?? 0;
        _saveService.Save(data);

        player.HealToFull();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(SpawnTransform.position, 0.4f);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(SpawnTransform.position, 0.4f);
    }
}
