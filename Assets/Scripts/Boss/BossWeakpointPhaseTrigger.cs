using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Fires a one-shot UnityEvent once a given number of weakpoints have been destroyed.
// Mirrors BossPhaseTrigger's shape but keys off BossWeakPointManager.WeakPointsChanged
// (an integer count) instead of Boss.HealthChanged (a float percent) — the boss has no
// gradually-decreasing HP, so weakpoint count is the real progress signal here.
public sealed class BossWeakpointPhaseTrigger : MonoBehaviour
{
    [System.Serializable]
    public class Phase
    {
        [Tooltip("Fires once this many weakpoints have been destroyed.")]
        public int destroyedThreshold;
        public UnityEvent onCrossed;
    }

    [SerializeField] private BossWeakPointManager weakPointManager;
    [SerializeField] private List<Phase> phases = new List<Phase>();

    private readonly HashSet<int> _fired = new HashSet<int>();

    private void OnEnable()
    {
        if (weakPointManager != null) weakPointManager.WeakPointsChanged += OnWeakPointsChanged;
    }

    private void OnDisable()
    {
        if (weakPointManager != null) weakPointManager.WeakPointsChanged -= OnWeakPointsChanged;
    }

    private void OnWeakPointsChanged()
    {
        int destroyed = weakPointManager.TotalWeakPoints - weakPointManager.AliveWeakPoints;
        for (int i = 0; i < phases.Count; i++)
        {
            if (_fired.Contains(i)) continue;
            if (destroyed >= phases[i].destroyedThreshold)
            {
                _fired.Add(i);
                phases[i].onCrossed?.Invoke();
            }
        }
    }
}
