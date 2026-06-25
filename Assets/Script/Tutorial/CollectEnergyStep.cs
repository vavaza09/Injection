using Game.Components.Skills;

namespace Game.Tutorial
{
    /// <summary>
    /// Completes when the player successfully collects one energy pickup.
    /// Resets the energy pool to zero on begin so the player must physically collect.
    /// </summary>
    public class CollectEnergyStep : TutorialStep
    {
        private IEnergyStore _store;

        protected override void OnBegin()
        {
            _store = Context?.EnergyStore;
            if (_store == null) return;

            _store.SetCurrent(0);
            _store.Changed += OnEnergyChanged;
        }

        private void OnEnergyChanged()
        {
            if (_store != null && _store.Current > 0)
                Complete();
        }

        protected override void OnCleanup()
        {
            if (_store != null)
                _store.Changed -= OnEnergyChanged;
        }
    }
}
