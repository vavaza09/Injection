using Game.Components.Movement;
using UnityEngine;

namespace Game.UI.Movement
{
    /// <summary>
    /// Reads current movement speed from MovementComponent and exposes it as km/h.
    /// Conversion factor maps game units/sec to a legible km/h range (SRP).
    /// </summary>
    public class MovementSpeedProvider : ISpeedDataProvider
    {
        // 1 game-unit/s → this many km/h (tune to taste)
        private const float UnitsPerSecToKmh = 10f;

        private readonly MovementComponent _movement;

        public float CurrentSpeedKmh { get; private set; }
        public float NormalizedSpeed { get; private set; }
        public float MaxSpeedKmh { get; private set; }

        /// <param name="movement">Source of velocity and max speed data.</param>
        public MovementSpeedProvider(MovementComponent movement)
        {
            _movement = movement;
            RecalculateMaxSpeed();
        }

        /// <summary>Recalculates speed values. Call once per frame from the HUD.</summary>
        public void Tick()
        {
            RecalculateMaxSpeed();

            float rawSpeed = _movement != null
                ? Mathf.Max(0f, _movement.CurrentMoveSpeed)
                : 0f;

            CurrentSpeedKmh = rawSpeed * UnitsPerSecToKmh;
            NormalizedSpeed = MaxSpeedKmh > 0f
                ? Mathf.Clamp01(CurrentSpeedKmh / MaxSpeedKmh)
                : 0f;
        }

        private void RecalculateMaxSpeed()
        {
            float maxGameUnitsPerSec = _movement != null ? Mathf.Max(0.01f, _movement.MaxSpeed) : 0f;
            MaxSpeedKmh = maxGameUnitsPerSec * UnitsPerSecToKmh;
        }
    }
}
