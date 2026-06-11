using UnityEngine;

namespace Game.Components.Skills
{
    public sealed class EmpBlastSkill : ISkill
    {
        public float EnergyCost { get; }
        public float CooldownRemaining => Mathf.Max(0f, _cooldown - (Time.time - _lastUseTime));
        public bool  CanActivate       => CooldownRemaining <= 0f && _energyPool.CanSpend(EnergyCost);

        private readonly float          _cooldown;
        private readonly float          _radius;
        private readonly float          _stunDuration;
        private readonly PlayerSkillEvents _bus;
        private readonly IEnergyPool    _energyPool;
        private readonly Core.Logging.ILogger _logger;
        private float                   _lastUseTime = float.NegativeInfinity;

        // context used at Activate time — set by PlayerSkillController before calling Activate
        private System.Func<Vector2>    _getPosition;

        public EmpBlastSkill(
            float cooldown,
            float radius,
            float stunDuration,
            float energyCost,
            PlayerSkillEvents bus,
            IEnergyPool energyPool,
            System.Func<Vector2> getPosition,
            Core.Logging.ILogger logger)
        {
            _cooldown     = cooldown;
            _radius       = radius;
            _stunDuration = stunDuration;
            EnergyCost    = energyCost;
            _bus          = bus;
            _energyPool   = energyPool;
            _getPosition  = getPosition;
            _logger       = logger;
        }

        public void Activate()
        {
            if (!CanActivate) return;
            _energyPool.TrySpend(EnergyCost);
            _lastUseTime = Time.time;

            var evt = new EmpBlastEvent(_getPosition(), _radius, _stunDuration);
            _bus.RaiseEmpDetonated(evt);
            _logger?.Log($"[EMP] Detonated at {evt.Position} radius={_radius} stun={_stunDuration}s");
        }

        public void Tick(float deltaTime) { }
    }
}
