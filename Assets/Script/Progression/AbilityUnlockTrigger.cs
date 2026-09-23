using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Game.Persistence;

namespace Game.Progression
{
    /// <summary>General-purpose permanent-unlock trigger — a pickup, comic-end hook, or
    /// boss-death event can all call <see cref="Unlock"/> (directly, or via
    /// <see cref="onUnlocked"/> from a UnityEvent) to flip a <see cref="AbilityIds"/> flag on
    /// permanently. Same collider-trigger + injected SaveService shape as SavePointTrigger.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class AbilityUnlockTrigger : MonoBehaviour
    {
        [SerializeField] private string abilityId = AbilityIds.Glide;
        [Tooltip("If true, entering this trigger's collider as the Player unlocks it. Leave " +
                 "false to only unlock via Unlock() from another event (comic end, boss death).")]
        [SerializeField] private bool unlockOnPlayerTrigger = true;

        [SerializeField] private UnityEvent onUnlocked;

        private SaveService _saveService;

        [Inject]
        public void Construct(SaveService saveService)
        {
            _saveService = saveService;
        }

        /// <summary>Idempotent — safe to call repeatedly (e.g. re-entering the trigger, or a
        /// UnityEvent firing more than once).</summary>
        public void Unlock()
        {
            if (_saveService == null || string.IsNullOrEmpty(abilityId)) return;
            if (_saveService.IsAbilityUnlocked(abilityId)) return;

            _saveService.MarkAbilityUnlocked(abilityId);
            onUnlocked?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!unlockOnPlayerTrigger) return;
            if (!other.CompareTag("Player")) return;
            Unlock();
        }
    }
}
