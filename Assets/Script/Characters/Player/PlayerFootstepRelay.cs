using UnityEngine;

namespace Game.Characters.Player
{
    // Sits on the same GameObject as the Animator to receive Animation Events,
    // then forwards to a callback wired by Player on init.
    public class PlayerFootstepRelay : MonoBehaviour
    {
        private System.Action _onFootstep;

        public void SetCallback(System.Action onFootstep) => _onFootstep = onFootstep;

        // Called by Animation Events in run.anim
        public void OnFootstep() => _onFootstep?.Invoke();
    }
}
