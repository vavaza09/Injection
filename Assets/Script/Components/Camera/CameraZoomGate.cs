using System;
using UnityEngine;

namespace Game.Components.CameraForesight
{
    /// <summary>
    /// Gate + dwell hysteresis for momentum zoom, mirroring <see cref="CameraDirectionalBias"/>'s
    /// anti-flicker pattern (same asymmetry: a fresh/just-<see cref="Reset"/> instance adopts its
    /// first sample immediately, dwell only governs changing an already-established state) but
    /// adapted for zoom's different semantics.
    ///
    /// Below the gate this recedes toward baseline (SpeedFactor 0) rather than holding the last
    /// engaged value the way bias holds its last direction: bias holds because "what's ahead" stays
    /// useful even at rest (e.g. standing at a ledge), but a camera that stays zoomed out while the
    /// player is standing still would read as broken, not helpful, so momentum should recede when
    /// the player actually stops.
    ///
    /// This only gates and debounces the effective SpeedFactor fed to
    /// <see cref="CameraForesightSolver.ComputeZoomOrthoSize"/> - the resulting zoom size stays
    /// continuous (curve-driven), never binary.
    /// </summary>
    public sealed class CameraZoomGate
    {
        private bool _engaged;
        private bool _hasEngagedState;
        private float _dwellTimer;

        /// <summary>
        /// Advances the gate hysteresis and returns the SpeedFactor to feed into
        /// <see cref="CameraForesightSolver.ComputeZoomOrthoSize"/> this frame: the live
        /// <paramref name="speedFactor"/> while the gate is engaged, or 0 while it is not. Call once
        /// per frame.
        /// </summary>
        public float Update(CameraProfile profile, float speedFactor, float deltaTime)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            bool passesGate = speedFactor >= profile.ZoomMinSpeedFactor;

            if (!_hasEngagedState)
            {
                // Nothing established yet (fresh instance, or just after Reset). Adopt immediately -
                // dwell governs changing an established state, not the first commitment.
                _engaged = passesGate;
                _hasEngagedState = true;
                _dwellTimer = 0f;
            }
            else if (passesGate == _engaged)
            {
                _dwellTimer = 0f;
            }
            else
            {
                _dwellTimer += Mathf.Max(0f, deltaTime);
                if (_dwellTimer >= profile.ZoomDwellTime)
                {
                    _engaged = passesGate;
                    _dwellTimer = 0f;
                }
            }

            return _engaged ? speedFactor : 0f;
        }

        /// <summary>
        /// Hard, complete state wipe - matches <see cref="CameraDirectionalBias.Reset"/>'s contract.
        /// The next <see cref="Update"/> adopts its sample immediately regardless of dwell, same as a
        /// fresh instance.
        /// </summary>
        public void Reset()
        {
            _engaged = false;
            _hasEngagedState = false;
            _dwellTimer = 0f;
        }
    }
}
