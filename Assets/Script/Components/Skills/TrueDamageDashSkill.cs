using UnityEngine;

namespace Game.Components.Skills
{
    public sealed class TrueDamageDashSkill : ISkill
    {
        public float EnergyCost { get; }
        public float CooldownRemaining => Mathf.Max(0f, _cooldown - (Time.time - _lastUseTime));
        public bool  CanActivate       => CooldownRemaining <= 0f
                                          && !_isArmed
                                          && _energyPool.CanSpend(EnergyCost);

        private readonly float             _cooldown;
        private readonly PlayerSkillEvents _bus;
        private readonly IEnergyPool       _energyPool;
        private readonly Core.Logging.ILogger _logger;
        // Reference to PlayerDashImpact — set by PlayerSkillController
        private System.Func<ITrueDamageTarget> _getDashImpact;

        private float _lastUseTime = float.NegativeInfinity;
        private bool  _isArmed;

        public TrueDamageDashSkill(
            float cooldown,
            float energyCost,
            PlayerSkillEvents bus,
            IEnergyPool energyPool,
            System.Func<ITrueDamageTarget> getDashImpact,
            Core.Logging.ILogger logger)
        {
            _cooldown      = cooldown;
            EnergyCost     = energyCost;
            _bus           = bus;
            _energyPool    = energyPool;
            _getDashImpact = getDashImpact;
            _logger        = logger;
        }

        public void Activate()
        {
            if (!CanActivate) return;
            _energyPool.TrySpend(EnergyCost);
            _lastUseTime = Time.time;
            _isArmed     = true;

            _getDashImpact()?.ArmTrueDamage();
            _bus.RaiseTrueDamageArmed();
            _logger?.Log("[TrueDash] Armed — next dash bypasses weak point");
        }

        // Called by PlayerDashImpact when the armed dash ends (hit or miss)
        public void OnConsumed()
        {
            if (!_isArmed) return;
            _isArmed     = false;
            _lastUseTime = Time.time;
            _bus.RaiseTrueDamageConsumed();
            _logger?.Log("[TrueDash] Consumed");
        }

        public void Tick(float deltaTime) { }
    }
}
