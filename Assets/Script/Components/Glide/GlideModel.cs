using System;

namespace Game.Components.Glide
{
    /// <summary>Pure state holder. Owned and mutated only by <see cref="GlideSystem"/> — views
    /// (companion sprite, Animator bool) read <see cref="State"/> and subscribe to
    /// <see cref="StateChanged"/>, never write it.</summary>
    public sealed class GlideModel
    {
        public GlideState State { get; private set; } = GlideState.Following;

        /// <summary>Raised whenever <see cref="State"/> actually changes (never on a no-op
        /// re-set to the same state). Carries the reason so views/audio can react differently to
        /// e.g. a voluntary Toggle vs. an involuntary Damaged end.</summary>
        public event Action<GlideState, GlideEndReason> StateChanged;

        internal void SetState(GlideState newState, GlideEndReason reason)
        {
            if (State == newState) return;
            State = newState;
            StateChanged?.Invoke(newState, reason);
        }
    }
}
