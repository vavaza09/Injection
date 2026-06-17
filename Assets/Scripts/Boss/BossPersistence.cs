using UnityEngine;
using VContainer;
using Game.Persistence;

public class BossPersistence : MonoBehaviour
{
    [SerializeField] private string bossId;

    private SaveService _saveService;

    [Inject]
    public void Construct(SaveService saveService)
    {
        _saveService = saveService;
    }

    private void Start()
    {
        if (_saveService != null && _saveService.IsBossDefeated(bossId))
        {
            Destroy(gameObject);
        }
    }

    public void MarkDefeated()
    {
        _saveService?.MarkBossDefeated(bossId);
    }
}
