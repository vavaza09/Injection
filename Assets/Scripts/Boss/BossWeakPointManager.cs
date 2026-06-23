using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach to the Boss root alongside BossAttackManager.
// Assign all 8 BossWeakPoint children in the Inspector.
// After every 2 completed attacks, reveals one random non-destroyed weakpoint in blue;
// the rest show yellow. If the player doesn't destroy it within windowTimeout seconds,
// the window closes and the cycle resets. Destroying all weakpoints kills the boss.
public class BossWeakPointManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossWeakPoint[]   weakPoints;
    [SerializeField] private Boss              boss;
    [SerializeField] private BossAttackManager attackManager;

    [Header("Reveal Timing")]
    [Tooltip("How many attacks the boss must complete before weakpoints reveal.")]
    [SerializeField] private int attacksToReveal = 2;
    [Tooltip("Seconds the player has to destroy the open weakpoint before it closes.")]
    [SerializeField] private float windowTimeout = 10f;

    private int           _attackCount;
    private bool          _windowOpen;
    private BossWeakPoint _openWeakPoint;
    private int           _destroyedCount;
    private Coroutine     _timeoutCoroutine;

    private void Start()
    {
        if (attackManager != null)
            attackManager.AttackCompleted += OnAttackCompleted;
    }

    private void OnDestroy()
    {
        if (attackManager != null)
            attackManager.AttackCompleted -= OnAttackCompleted;
    }

    private void OnAttackCompleted()
    {
        if (_windowOpen) return;
        _attackCount++;
        if (_attackCount >= attacksToReveal)
            RevealRandom();
    }

    private void RevealRandom()
    {
        var available = new List<BossWeakPoint>();
        foreach (var wp in weakPoints)
            if (wp != null && !wp.IsDestroyed) available.Add(wp);

        if (available.Count == 0) return;

        _openWeakPoint = available[Random.Range(0, available.Count)];
        _openWeakPoint.OnDestroyed += OnWeakPointDestroyed;
        _openWeakPoint.Show(attackable: true);

        foreach (var wp in weakPoints)
            if (wp != null && !wp.IsDestroyed && wp != _openWeakPoint)
                wp.Show(attackable: false);

        _windowOpen  = true;
        _attackCount = 0;

        _timeoutCoroutine = StartCoroutine(WindowTimeout());
    }

    private IEnumerator WindowTimeout()
    {
        yield return new WaitForSeconds(windowTimeout);

        // Player didn't hit the weakpoint in time — close the window.
        if (_windowOpen)
            CloseWindow();
    }

    private void CloseWindow()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        if (_openWeakPoint != null)
        {
            _openWeakPoint.OnDestroyed -= OnWeakPointDestroyed;
            _openWeakPoint = null;
        }

        foreach (var wp in weakPoints)
            if (wp != null && !wp.IsDestroyed)
                wp.Hide();

        _windowOpen  = false;
        _attackCount = 0;
    }

    private void OnWeakPointDestroyed()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        if (_openWeakPoint != null)
            _openWeakPoint.OnDestroyed -= OnWeakPointDestroyed;
        _openWeakPoint = null;

        foreach (var wp in weakPoints)
            if (wp != null && !wp.IsDestroyed)
                wp.Hide();

        _destroyedCount++;
        _windowOpen  = false;
        _attackCount = 0;

        if (CountAlive() == 0)
            boss?.TakeDamage(int.MaxValue);
    }

    private int CountAlive()
    {
        int count = 0;
        foreach (var wp in weakPoints)
            if (wp != null && !wp.IsDestroyed) count++;
        return count;
    }
}
