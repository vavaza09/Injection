using Game.Components.Skills;
using UnityEngine;
using VContainer;

public class PlayerEnergyCollector : MonoBehaviour
{
    [SerializeField] private float collectRadius = 1.5f;

    private IEnergyStore _store;
    private Core.Logging.ILogger _logger;

    [Inject]
    public void Construct(Core.Logging.LoggerFactory loggerFactory, IEnergyStore energyStore)
    {
        _logger = loggerFactory?.CreateLogger<PlayerEnergyCollector>();
        _store  = energyStore;
    }

    public bool TryCollectNearby()
    {
        if (_store == null || _store.IsFull) return false;

        var hits = Physics2D.OverlapCircleAll(transform.position, collectRadius);
        foreach (var hit in hits)
        {
            var pickup = hit.GetComponent<Game.Components.Skills.EnergyPickup>();
            if (pickup == null || !pickup.IsAvailable) continue;

            if (pickup.TryCollect(out int amount))
            {
                bool added = _store.TryAdd(amount);
                _logger?.Log($"[Energy] Collected {amount} — now {_store.Current}/{_store.Max}");
                return added;
            }
        }

        return false;
    }
}
