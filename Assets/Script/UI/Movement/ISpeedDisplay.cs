namespace Game.UI.Movement
{
    /// <summary>
    /// Abstraction for any component that can render speed data visually.
    /// Enables adding new display types without modifying the HUD (OCP).
    /// </summary>
    public interface ISpeedDisplay
    {
        /// <summary>Push updated speed data to the display.</summary>
        void Refresh(ISpeedDataProvider provider);

        /// <summary>Show or hide this display.</summary>
        void SetVisible(bool visible);
    }
}
