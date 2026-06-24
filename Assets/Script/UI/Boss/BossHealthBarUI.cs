using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private Image           fillImage;
    [SerializeField] private GameObject      barRoot;
    [SerializeField] private TextMeshProUGUI titleLabel;

    private BossWeakPointManager _manager;

    public void Bind(BossWeakPointManager manager)
    {
        if (_manager != null)
            _manager.WeakPointsChanged -= Refresh;

        _manager = manager;
        _manager.WeakPointsChanged += Refresh;
        Refresh();
        Hide();
    }

    public void Show() => barRoot?.SetActive(true);
    public void Hide() => barRoot?.SetActive(false);

    private void Refresh()
    {
        if (_manager == null || fillImage == null) return;

        int total = _manager.TotalWeakPoints;
        int alive = _manager.AliveWeakPoints;
        fillImage.fillAmount = total > 0 ? (float)alive / total : 0f;

        if (titleLabel != null)
            titleLabel.text = alive > 0 ? "BOSS" : "DEFEATED";
    }

    private void OnDestroy()
    {
        if (_manager != null)
            _manager.WeakPointsChanged -= Refresh;
    }
}
