namespace Game.Components.Glide
{
    /// <summary>Immutable snapshot of every player-movement fact <see cref="GlideSystem"/> needs
    /// to decide whether glide can start and whether it must auto-end. Built by the glue layer
    /// (<c>PlayerGlideController</c>) from <c>MovementComponent</c>/<c>character</c> each frame —
    /// this type itself has zero engine or MonoBehaviour dependency.</summary>
    public readonly struct GlideContext
    {
        /// <summary>Not grounded and not already doing something else airborne (wall slide,
        /// grab, dash) — mirrors <c>MovementComponent.IsAirborne</c>. Glide only requires this
        /// (plus not having an available ground jump) — it does NOT require already falling, so
        /// it can start the instant the player is airborne, including still rising after a jump.</summary>
        public readonly bool IsAirborne;

        /// <summary>True while an ordinary ground/coyote jump is still available — glide must
        /// never intercept a Space press that would otherwise jump.</summary>
        public readonly bool CanGroundJump;

        public readonly bool IsDashing;
        public readonly bool IsWallSliding;
        public readonly bool IsGrabbing;
        public readonly bool IsStunned;
        public readonly bool IsCaptured;

        /// <summary>Currently in a hit-reaction knockback window.</summary>
        public readonly bool IsKnocked;

        public readonly bool IsAlive;

        public GlideContext(
            bool isAirborne,
            bool canGroundJump,
            bool isDashing,
            bool isWallSliding,
            bool isGrabbing,
            bool isStunned,
            bool isCaptured,
            bool isKnocked,
            bool isAlive)
        {
            IsAirborne = isAirborne;
            CanGroundJump = canGroundJump;
            IsDashing = isDashing;
            IsWallSliding = isWallSliding;
            IsGrabbing = isGrabbing;
            IsStunned = isStunned;
            IsCaptured = isCaptured;
            IsKnocked = isKnocked;
            IsAlive = isAlive;
        }
    }
}
