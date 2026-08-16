using UnityEngine;
using Game.Characters.Player;

namespace Game.UI
{
    /// <summary>Finds the scene's Player and enables/disables its input. Shared by every
    /// screen that needs to suspend gameplay input (pause menu, victory screen, comics, ...) —
    /// previously duplicated verbatim in each controller.</summary>
    public static class PlayerInputGate
    {
        public static void Set(bool enabled)
        {
            var playerGO = GameObject.FindWithTag("Player");
            playerGO?.GetComponent<Player>()?.SetInputEnabled(enabled);
        }
    }
}
