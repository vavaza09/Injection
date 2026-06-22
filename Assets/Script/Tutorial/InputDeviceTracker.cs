using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tutorial
{
    public enum InputDeviceKind { Keyboard, Gamepad }

    /// <summary>
    /// Tracks which input device the player last used so the tutorial prompt UI can live-switch
    /// keyboard/mouse glyphs to gamepad glyphs (and back) the instant the player swaps device.
    /// </summary>
    public class InputDeviceTracker : MonoBehaviour
    {
        public InputDeviceKind Current { get; private set; } = InputDeviceKind.Keyboard;
        public event Action<InputDeviceKind> DeviceChanged;

        private void OnEnable()  => InputSystem.onActionChange += OnActionChange;
        private void OnDisable() => InputSystem.onActionChange -= OnActionChange;

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            if (obj is not InputAction action) return;

            var device = action.activeControl?.device;
            if (device == null) return;

            InputDeviceKind kind;
            if (device is Gamepad) kind = InputDeviceKind.Gamepad;
            else if (device is Keyboard || device is Mouse) kind = InputDeviceKind.Keyboard;
            else return; // ignore other devices

            if (kind == Current) return;
            Current = kind;
            DeviceChanged?.Invoke(kind);
        }
    }
}
