using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// Completes when the practice dummy dies. Shows a weak-point indicator while active and
    /// disables the dummy's AI so it never retaliates — the player can focus on aiming a dash
    /// into the weak point (the only way a normal dash deals damage; see PlayerDashImpact).
    /// </summary>
    public class KillEnemyStep : TutorialStep
    {
        [SerializeField] private Enemy dummy;
        [Tooltip("Component that drives the dummy's attacks (EnemyAI, or the enemy script itself for types without a separate AI). Disabled while the step runs so the dummy never retaliates.")]
        [SerializeField] private Behaviour aiBehaviour;
        [Tooltip("Glowing-ring indicator GO placed over the dummy's weak point. Toggled with the step.")]
        [SerializeField] private GameObject weakPointIndicator;

        protected override void OnBegin()
        {
            if (aiBehaviour != null) aiBehaviour.enabled = false;
            if (weakPointIndicator != null) weakPointIndicator.SetActive(true);
            if (dummy != null) dummy.Died += OnDummyDied;
        }

        private void OnDummyDied(character c)
        {
            Complete();
        }

        protected override void OnCleanup()
        {
            if (dummy != null) dummy.Died -= OnDummyDied;
            if (weakPointIndicator != null) weakPointIndicator.SetActive(false);
        }
    }
}
