using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private Image           fillImage;
    [SerializeField] private GameObject      barRoot;
    [SerializeField] private TextMeshProUGUI titleLabel;

    [SerializeField] private float nameFadeDuration  = 0.4f;
    [SerializeField] private float fillSlideDuration = 0.5f;

    private BossWeakPointManager _manager;
    private Coroutine            _revealRoutine;

    public void Bind(BossWeakPointManager manager)
    {
        if (_manager != null)
            _manager.WeakPointsChanged -= Refresh;

        _manager = manager;
        _manager.WeakPointsChanged += Refresh;
        Refresh();
        Hide();
    }

    public void Show()
    {
        if (barRoot == null) return;
        barRoot.SetActive(true);

        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(RevealRoutine());
    }

    public void Hide()
    {
        if (_revealRoutine != null) { StopCoroutine(_revealRoutine); _revealRoutine = null; }
        if (titleLabel != null) titleLabel.alpha = 1f;
        if (barRoot != null) barRoot.SetActive(false);
    }

    private IEnumerator RevealRoutine()
    {
        if (titleLabel != null) titleLabel.alpha = 0f;
        if (fillImage  != null) fillImage.fillAmount = 0f;

        float duration = Mathf.Max(nameFadeDuration, fillSlideDuration);
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (titleLabel != null)
                titleLabel.alpha = nameFadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / nameFadeDuration);
            if (fillImage != null)
            {
                float t = fillSlideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fillSlideDuration);
                fillImage.fillAmount = Mathf.Lerp(0f, CurrentFill(), t);
            }
            yield return null;
        }

        if (titleLabel != null) titleLabel.alpha = 1f;
        if (fillImage  != null) fillImage.fillAmount = CurrentFill();
        _revealRoutine = null;
    }

    private float CurrentFill()
    {
        if (_manager == null || _manager.TotalWeakPoints <= 0) return 0f;
        return (float)_manager.AliveWeakPoints / _manager.TotalWeakPoints;
    }

    private void Refresh()
    {
        if (_manager == null || fillImage == null) return;

        int total = _manager.TotalWeakPoints;
        int alive = _manager.AliveWeakPoints;
        fillImage.fillAmount = total > 0 ? (float)alive / total : 0f;

        if (titleLabel != null)
            titleLabel.text = alive > 0 ? "THE COLLECTOR" : "DEFEATED";
    }

    private void OnDestroy()
    {
        if (_manager != null)
            _manager.WeakPointsChanged -= Refresh;
    }
}
