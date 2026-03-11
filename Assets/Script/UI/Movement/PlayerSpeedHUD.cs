using System.Collections.Generic;
using Game.Components.Movement;
using UnityEngine;

namespace Game.UI.Movement
{
    /// <summary>
    /// Orchestrates all speed UI displays for the local player (SRP — wiring only).
    /// Add this MonoBehaviour to the HUD Canvas. Assign MovementComponent via Inspector
    /// or call Initialize() at runtime.
    ///
    /// New display types can be registered without modifying this class (OCP).
    /// </summary>
    public class PlayerSpeedHUD : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private MovementComponent movementComponent;
        [SerializeField] private float maxMoveSpeedUnits = 8f;

        [Header("Displays (add any ISpeedDisplay components here)")]
        [SerializeField] private SpeedTextDisplay textDisplay;
        [SerializeField] private SpeedDashboardDisplay dashboardDisplay;

        private MovementSpeedProvider _provider;
        private readonly List<ISpeedDisplay> _displays = new List<ISpeedDisplay>();

        private void Awake()
        {
            if (textDisplay != null) _displays.Add(textDisplay);
            if (dashboardDisplay != null) _displays.Add(dashboardDisplay);
        }

        private void Start()
        {
            if (movementComponent != null)
                Initialize(movementComponent, maxMoveSpeedUnits);
        }

        /// <summary>
        /// Programmatic initialization (call from Player or DI if needed).
        /// </summary>
        public void Initialize(MovementComponent movement, float maxSpeedUnits)
        {
            _provider = new MovementSpeedProvider(movement, maxSpeedUnits);
        }

        /// <summary>
        /// Register an additional display at runtime without modifying this class (OCP).
        /// </summary>
        public void RegisterDisplay(ISpeedDisplay display)
        {
            if (display != null && !_displays.Contains(display))
                _displays.Add(display);
        }

        public void UnregisterDisplay(ISpeedDisplay display)
        {
            _displays.Remove(display);
        }

        private void Update()
        {
            if (_provider == null) return;

            _provider.Tick();

            foreach (var display in _displays)
                display.Refresh(_provider);
        }

        public void SetHUDVisible(bool visible)
        {
            foreach (var display in _displays)
                display.SetVisible(visible);
        }
    }
}
