namespace Game.Components.Glide
{
    /// <summary>Owns and mutates <see cref="GlideModel"/>. All glide decision logic lives here —
    /// the glue layer (<c>PlayerGlideController</c>) only builds a <see cref="GlideContext"/> and
    /// reads <see cref="FallSpeedCap"/>/<see cref="FallSpeedCapEaseRate"/> back out.</summary>
    public sealed class GlideSystem
    {
        private readonly GlideModel _model;
        private readonly GlideConfig _config;

        public GlideSystem(GlideModel model, GlideConfig config)
        {
            _model = model;
            _config = config;
        }

        /// <summary>Target fall-speed magnitude to write onto
        /// <c>MovementComponent.FallSpeedLimitOverride</c>. <c>+Infinity</c> while Following
        /// (no restriction) — that value is returned instantly on any end reason, so an
        /// involuntary end (damage, landing) always feels immediate.</summary>
        public float FallSpeedCap =>
            _model.State == GlideState.Gliding ? _config.GlideMaxFallSpeed : float.PositiveInfinity;

        /// <summary>Pairs with <see cref="FallSpeedCap"/> — write onto
        /// <c>MovementComponent.FallSpeedLimitEaseRate</c>. Only smooths the engage transition;
        /// irrelevant once <see cref="FallSpeedCap"/> itself is <c>+Infinity</c>.</summary>
        public float FallSpeedCapEaseRate => _config.GlideCapBrakeStrength;

        /// <summary>Pure predicate: can glide start from this context? Exposed static so it can
        /// be unit-tested directly without constructing a full system/model.</summary>
        public static bool CanStartGlide(in GlideContext ctx, bool isUnlocked) =>
            isUnlocked
            && ctx.IsAlive
            && ctx.IsAirborne
            && ctx.IsFalling
            && !ctx.CanGroundJump
            && !ctx.IsDashing
            && !ctx.IsWallSliding
            && !ctx.IsGrabbing
            && !ctx.IsStunned
            && !ctx.IsCaptured
            && !ctx.IsKnocked;

        /// <summary>Call once per Space press. <paramref name="previousFrameContext"/> MUST be a
        /// snapshot taken before this frame's press was processed (e.g. last frame's
        /// <c>LateUpdate</c>) — never a live read — so a same-frame grab-launch or ground jump
        /// can never race the toggle. See the "Fixed after critic review" note in the feature
        /// brief for the concrete race this prevents.</summary>
        public void RequestToggle(in GlideContext previousFrameContext, bool isUnlocked)
        {
            if (_model.State == GlideState.Gliding)
            {
                _model.SetState(GlideState.Following, GlideEndReason.Toggle);
                return;
            }

            if (CanStartGlide(previousFrameContext, isUnlocked))
            {
                _model.SetState(GlideState.Gliding, GlideEndReason.Toggle);
            }
        }

        /// <summary>Call every frame with the CURRENT context. Applies every auto-end rule;
        /// no-ops while already Following.</summary>
        public void Tick(in GlideContext ctx)
        {
            if (_model.State != GlideState.Gliding) return;

            GlideEndReason? endReason = ResolveAutoEndReason(ctx);
            if (endReason.HasValue)
            {
                _model.SetState(GlideState.Following, endReason.Value);
            }
        }

        private static GlideEndReason? ResolveAutoEndReason(in GlideContext ctx)
        {
            if (!ctx.IsAlive) return GlideEndReason.Dead;
            if (!ctx.IsAirborne) return GlideEndReason.Landed;
            if (ctx.IsDashing) return GlideEndReason.Dash;
            if (ctx.IsWallSliding) return GlideEndReason.Wall;
            if (ctx.IsGrabbing) return GlideEndReason.Grab;
            if (ctx.IsStunned) return GlideEndReason.Stunned;
            if (ctx.IsCaptured) return GlideEndReason.Captured;
            if (ctx.IsKnocked) return GlideEndReason.Damaged;
            return null;
        }
    }
}
