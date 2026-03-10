namespace Game.UI.Movement
{
    /// <summary>
    /// Provides normalized and raw speed data to UI displays.
    /// Abstraction layer between movement physics and UI (DIP).
    /// </summary>
    public interface ISpeedDataProvider
    {
        /// <summary>Current speed in km/h.</summary>
        float CurrentSpeedKmh { get; }

        /// <summary>Speed normalized to [0, 1] relative to max speed.</summary>
        float NormalizedSpeed { get; }

        /// <summary>Max speed in km/h used for normalization.</summary>
        float MaxSpeedKmh { get; }
    }
}
