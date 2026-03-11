using Game.Components.Movement;
using UnityEngine;

namespace Game.UI.Movement
{
    /// <summary>
    /// Reads velocity from MovementComponent and exposes it as km/h.
    /// Conversion factor maps game units/sec to a legible km/h range (SRP).
    /// </summary>
    public class MovementSpeedProvider : ISpeedDataProvider
    {
        // 1 game-unit/s → this many km/h (tune to taste)
        private const float UnitsPerSecToKmh = 10f;

        private readonly MovementComponent _movement;
        private readonly float _maxSpeedKmh;

        public float CurrentSpeedKmh { get; private set; }
        public float NormalizedSpeed { get; private set; }
        public float MaxSpeedKmh => _maxSpeedKmh;

        /// <param name="movement">Source of velocity data.</param>
        /// <param name="maxGameUnitsPerSec">Max movement speed in game units/s (e.g. moveSpeed field).</param>
        public MovementSpeedProvider(MovementComponent movement, float maxGameUnitsPerSec)
        {
            _movement = movement;
            _maxSpeedKmh = maxGameUnitsPerSec * UnitsPerSecToKmh;
        }

        /// <summary>Recalculates speed values. Call once per frame from the HUD.</summary>
        public void Tick()
        {
            float rawSpeed = _movement != null
                ? Mathf.Abs(_movement.GetVelocity().x)
                : 0f;

            CurrentSpeedKmh = rawSpeed * UnitsPerSecToKmh;
            NormalizedSpeed = _maxSpeedKmh > 0f
                ? Mathf.Clamp01(CurrentSpeedKmh / _maxSpeedKmh)
                : 0f;
        }
    }
}
