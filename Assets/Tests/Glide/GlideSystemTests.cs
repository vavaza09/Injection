using NUnit.Framework;
using UnityEngine;

namespace Game.Components.Glide.EditModeTests
{
    public class GlideSystemTests
    {
        private GlideModel _model;
        private GlideConfig _config;
        private GlideSystem _sut;

        [SetUp]
        public void Setup()
        {
            _model = new GlideModel();
            _config = ScriptableObject.CreateInstance<GlideConfig>();
            _sut = new GlideSystem(_model, _config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        // Defaults describe a context where glide SHOULD be able to start: airborne, no ground
        // jump available, not doing anything else, alive. Every parameter is named and optional
        // so a test only has to override the one fact it's exercising.
        private static GlideContext MakeContext(
            bool isAirborne = true,
            bool canGroundJump = false,
            bool isDashing = false,
            bool isWallSliding = false,
            bool isGrabbing = false,
            bool isStunned = false,
            bool isCaptured = false,
            bool isKnocked = false,
            bool isAlive = true)
            => new GlideContext(isAirborne, canGroundJump, isDashing, isWallSliding,
                isGrabbing, isStunned, isCaptured, isKnocked, isAlive);

        // --- CanStartGlide ---

        [Test]
        public void CanStartGlide_AllConditionsMet_ReturnsTrue()
        {
            Assert.IsTrue(GlideSystem.CanStartGlide(MakeContext(), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Locked_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(), isUnlocked: false));
        }

        // No CanStartGlide_Rising_* test: glide is deliberately vertical-direction-agnostic —
        // GlideContext doesn't even track rising vs. falling — so CanStartGlide_AllConditionsMet
        // already covers "airborne, whichever way you're currently moving" on its own.

        [Test]
        public void CanStartGlide_GroundJumpAvailable_ReturnsFalse()
        {
            // Space must jump, not glide, while a ground/coyote jump is still available.
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(canGroundJump: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Dashing_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isDashing: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_WallSliding_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isWallSliding: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Grabbing_ReturnsFalse()
        {
            // Regression for the same-frame grab-launch race: PlayerGlideController must judge
            // the toggle against a context where IsGrabbing still reflects "was grabbing" —
            // this predicate itself must reject grabbing regardless of who calls it or when.
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isGrabbing: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Stunned_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isStunned: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Captured_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isCaptured: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Knocked_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isKnocked: true), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_Dead_ReturnsFalse()
        {
            Assert.IsFalse(GlideSystem.CanStartGlide(MakeContext(isAlive: false), isUnlocked: true));
        }

        [Test]
        public void CanStartGlide_ImmediatelyAfterJumpLaunch_ReturnsTrue()
        {
            // Regression for the explicit design change: glide no longer waits for the apex.
            // The instant a jump launches, MovementComponent.BeginJumpWindow() zeroes the coyote
            // timer, so CanGroundJump is already false — that's the only gate that matters here.
            var justLaunched = MakeContext(isAirborne: true, canGroundJump: false);
            Assert.IsTrue(GlideSystem.CanStartGlide(justLaunched, isUnlocked: true));
        }

        // --- RequestToggle ---

        [Test]
        public void RequestToggle_FromFollowing_EligibleContext_StartsGliding()
        {
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            Assert.AreEqual(GlideState.Gliding, _model.State);
        }

        [Test]
        public void RequestToggle_FromFollowing_FiresStateChangedWithToggleReason()
        {
            GlideEndReason? capturedReason = null;
            _model.StateChanged += (_, reason) => capturedReason = reason;

            _sut.RequestToggle(MakeContext(), isUnlocked: true);

            Assert.AreEqual(GlideEndReason.Toggle, capturedReason);
        }

        [Test]
        public void RequestToggle_WhileGliding_TogglesOffRegardlessOfContext()
        {
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            Assume.That(_model.State, Is.EqualTo(GlideState.Gliding));

            // Even a context that would never have STARTED glide (e.g. now grounded) still ends
            // it via the toggle path — toggling off is unconditional.
            _sut.RequestToggle(MakeContext(isAirborne: false, canGroundJump: true), isUnlocked: true);

            Assert.AreEqual(GlideState.Following, _model.State);
        }

        [Test]
        public void RequestToggle_SameFrameGrabLaunchRace_CannotStartGlide()
        {
            // The concrete race the "Fixed after critic review" note describes: Player.Jump()
            // can launch off a grab and flip IsGrabbing false in the same input callback that
            // also delivers this press to the glide controller. PlayerGlideController's fix is
            // to always pass the PREVIOUS frame's context (still IsGrabbing == true here) rather
            // than a live read — verify the system respects whatever context it's given.
            var previousFrameContext = MakeContext(isGrabbing: true);

            _sut.RequestToggle(previousFrameContext, isUnlocked: true);

            Assert.AreEqual(GlideState.Following, _model.State,
                "A press judged against a still-grabbing snapshot must not start glide.");
        }

        // --- Tick / auto-end ---

        [Test]
        public void Tick_WhileFollowing_NeverFiresStateChanged()
        {
            bool fired = false;
            _model.StateChanged += (state, reason) => fired = true;

            _sut.Tick(MakeContext());

            Assert.IsFalse(fired);
        }

        [Test]
        public void Tick_StillEligible_StaysGliding()
        {
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            _sut.Tick(MakeContext());
            Assert.AreEqual(GlideState.Gliding, _model.State);
        }

        [Test]
        public void Tick_Landed_EndsGlideWithLandedReason()
        {
            AssertAutoEnd(MakeContext(isAirborne: false), GlideEndReason.Landed);
        }

        [Test]
        public void Tick_Dead_EndsGlideWithDeadReason()
        {
            AssertAutoEnd(MakeContext(isAlive: false), GlideEndReason.Dead);
        }

        [Test]
        public void Tick_Dashing_EndsGlideWithDashReason()
        {
            AssertAutoEnd(MakeContext(isDashing: true), GlideEndReason.Dash);
        }

        [Test]
        public void Tick_WallSliding_EndsGlideWithWallReason()
        {
            AssertAutoEnd(MakeContext(isWallSliding: true), GlideEndReason.Wall);
        }

        [Test]
        public void Tick_Grabbing_EndsGlideWithGrabReason()
        {
            AssertAutoEnd(MakeContext(isGrabbing: true), GlideEndReason.Grab);
        }

        [Test]
        public void Tick_Stunned_EndsGlideWithStunnedReason()
        {
            AssertAutoEnd(MakeContext(isStunned: true), GlideEndReason.Stunned);
        }

        [Test]
        public void Tick_Captured_EndsGlideWithCapturedReason()
        {
            AssertAutoEnd(MakeContext(isCaptured: true), GlideEndReason.Captured);
        }

        [Test]
        public void Tick_Knocked_EndsGlideWithDamagedReason()
        {
            AssertAutoEnd(MakeContext(isKnocked: true), GlideEndReason.Damaged);
        }

        [Test]
        public void Tick_DeadTakesPrecedenceOverOtherEndConditions()
        {
            // Precedence matters for the reason reported, even though every listed condition
            // ends glide either way — Dead should win when multiple are true simultaneously.
            AssertAutoEnd(MakeContext(isAlive: false, isDashing: true, isKnocked: true), GlideEndReason.Dead);
        }

        private void AssertAutoEnd(GlideContext endingContext, GlideEndReason expectedReason)
        {
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            Assume.That(_model.State, Is.EqualTo(GlideState.Gliding));

            GlideEndReason? capturedReason = null;
            _model.StateChanged += (_, reason) => capturedReason = reason;

            _sut.Tick(endingContext);

            Assert.AreEqual(GlideState.Following, _model.State);
            Assert.AreEqual(expectedReason, capturedReason);
        }

        // --- FallSpeedCap / FallSpeedCapEaseRate ---

        [Test]
        public void FallSpeedCap_WhileFollowing_IsPositiveInfinity()
        {
            Assert.IsTrue(float.IsPositiveInfinity(_sut.FallSpeedCap));
        }

        [Test]
        public void FallSpeedCap_WhileGliding_EqualsConfiguredMax()
        {
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            Assert.AreEqual(_config.GlideMaxFallSpeed, _sut.FallSpeedCap);
        }

        [Test]
        public void FallSpeedCap_AfterEnding_ReturnsToPositiveInfinityImmediately()
        {
            // Ending must be instant (not eased) so involuntary ends (damage, landing) feel
            // responsive — only the ENGAGE transition eases, via FallSpeedCapEaseRate applied
            // where the cap is actually consumed (MovementComponent.ApplyJumpGravity).
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            _sut.RequestToggle(MakeContext(), isUnlocked: true); // toggle off

            Assert.IsTrue(float.IsPositiveInfinity(_sut.FallSpeedCap));
        }

        [Test]
        public void FallSpeedCapEaseRate_AlwaysEqualsConfiguredBrakeStrength()
        {
            Assert.AreEqual(_config.GlideCapBrakeStrength, _sut.FallSpeedCapEaseRate);
            _sut.RequestToggle(MakeContext(), isUnlocked: true);
            Assert.AreEqual(_config.GlideCapBrakeStrength, _sut.FallSpeedCapEaseRate);
        }
    }
}
