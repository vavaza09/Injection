using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Game.Rooms.Objectives;

public sealed class BossPhaseTrigger : MonoBehaviour
{
    [Serializable]
    public class Phase
    {
        [Range(0f, 1f)]
        public float healthThreshold = 0.5f;
        public UnityEvent onCrossed;
    }

    [SerializeField] private BossBase boss;
    [SerializeField] private List<Phase> phases = new List<Phase>();

    private PhaseThresholdTracker _tracker;

    private void Awake()
    {
        var thresholds = new List<float>(phases.Count);
        foreach (var p in phases) thresholds.Add(p.healthThreshold);
        _tracker = new PhaseThresholdTracker(thresholds);
    }

    private void OnEnable()
    {
        if (boss != null) boss.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (boss != null) boss.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float percent)
    {
        int idx = _tracker.CheckPercent(percent);
        if (idx >= 0 && idx < phases.Count)
            phases[idx].onCrossed?.Invoke();
    }
}
