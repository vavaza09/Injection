namespace Game.Components.Glide
{
    /// <summary>Why a <see cref="GlideState.Gliding"/>→<see cref="GlideState.Following"/>
    /// transition happened. Also used as the reason for the initial Following state and for a
    /// player-initiated Toggle-on, so every <c>StateChanged</c> invocation carries a reason.</summary>
    public enum GlideEndReason
    {
        /// <summary>Player pressed Space (covers both toggle-on and toggle-off).</summary>
        Toggle = 0,
        Landed = 1,
        Dash = 2,
        Wall = 3,
        Grab = 4,
        Stunned = 5,
        Captured = 6,
        Damaged = 7,
        Dead = 8
    }
}
