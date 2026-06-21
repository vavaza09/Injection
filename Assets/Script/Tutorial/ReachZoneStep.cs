using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// Completes when the player enters this object's trigger collider. Used for walk/jump/dash/
    /// wall-jump/swing steps — the level layout is built so the zone is only reachable by using
    /// the ability taught in that step.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ReachZoneStep : TutorialStep
    {
        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive) return;
            if (other.GetComponentInParent<Player>() == null) return;
            Complete();
        }
    }
}
