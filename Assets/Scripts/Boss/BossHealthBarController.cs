using UnityEngine;

public class BossHealthBarController : MonoBehaviour
{
    [SerializeField] private GameObject           healthBarPrefab;
    [SerializeField] private BossWeakPointManager weakPointManager;
    [SerializeField] private BossBase             boss;

    private BossHealthBarUI _ui;
    private GameObject      _hudInstance;

    private void Start()
    {
        if (healthBarPrefab == null || weakPointManager == null || boss == null)
        {
            Debug.LogWarning("[BossHealthBarController] Missing reference — HP bar won't show.", this);
            return;
        }

        _hudInstance = Instantiate(healthBarPrefab);
        _ui          = _hudInstance.GetComponentInChildren<BossHealthBarUI>(true);

        if (_ui == null)
        {
            Debug.LogWarning("[BossHealthBarController] BossHealthBarUI not found in prefab.", this);
            return;
        }

        _ui.Bind(weakPointManager);

        boss.PlayerEnteredRange += _ui.Show;
        boss.PlayerExitedRange  += _ui.Hide;

        if (boss.IsPlayerCurrentlyInRange)
            _ui.Show();
    }

    private void OnDestroy()
    {
        if (boss != null)
        {
            boss.PlayerEnteredRange -= _ui.Show;
            boss.PlayerExitedRange  -= _ui.Hide;
        }

        if (_hudInstance != null)
            Destroy(_hudInstance);
    }
}
