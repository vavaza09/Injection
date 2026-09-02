using System;
using Game.Components.CameraForesight;
using NUnit.Framework;
using UnityEngine;

namespace Game.Camera.EditModeTests
{
    public class CameraDirectionalBiasTests
    {
        private const float MAX_DISTANCE = 5.5f;
        private const float MIN_SPEED_FACTOR = 0.5f;
        private const float DWELL_TIME = 0.4f;

        private const float FRAME = 0.1f;
        private const float FAST = 1f;
        private const float SLOW = 0.1f;

        private static readonly Vector2 RunRight = new Vector2(30f, 0f);
        private static readonly Vector2 RunLeft = new Vector2(-30f, 0f);

        private CameraDirectionalBias _bias;
        private CameraProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _bias = new CameraDirectionalBias();
            _profile = TestCameraProfile.Create(
                biasMaxDistance: MAX_DISTANCE,
                biasMinSpeedFactor: MIN_SPEED_FACTOR,
                biasDwellTime: DWELL_TIME);
        }

        [TearDown]
        public void TearDown()
        {
            TestCameraProfile.DestroyAll();
        }

        // --- Establishing a direction ---

        [Test]
        public void Update_SustainedRightwardLongerThanDwell_ReachesFullBiasDistanceRight()
        {
            Vector2 result = Run(RunRight, FAST, FramesFor(DWELL_TIME * 3f));

            Assert.AreEqual(MAX_DISTANCE, result.x, 0.0001f);
            Assert.AreEqual(0f, result.y, 0.0001f, "bias is horizontal only; look-down owns vertical");
        }

        [Test]
        public void Update_SustainedLeftwardLongerThanDwell_ReachesFullBiasDistanceLeft()
        {
            Vector2 result = Run(RunLeft, FAST, FramesFor(DWELL_TIME * 3f));

            Assert.AreEqual(-MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_FreshInstance_EmitsNothingUntilAQualifyingSample()
        {
            Assert.AreEqual(Vector2.zero, _bias.Update(_profile, Vector2.zero, 0f, FRAME));
        }

        [Test]
        public void Update_FirstQualifyingSample_EstablishesDirectionImmediately()
        {
            // The dwell time guards FLIPS, not the initial commitment - otherwise the
            // camera would lag behind the player's very first run of the level.
            Vector2 result = _bias.Update(_profile, RunRight, FAST, FRAME);

            Assert.AreEqual(MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_BelowMinSpeedFactor_DoesNotEstablishADirection()
        {
            // Fast enough horizontally in raw units, but the normalised factor is under
            // the gate: slow shuffling must not push the camera off-centre.
            Vector2 result = Run(RunRight, SLOW, 20);

            Assert.AreEqual(Vector2.zero, result);
        }

        [Test]
        public void Update_PurelyVerticalVelocity_DoesNotEstablishADirection()
        {
            // Fast fall / straight-up wall jump has no horizontal intent to read.
            Vector2 result = Run(new Vector2(0f, 50f), FAST, 20);

            Assert.AreEqual(Vector2.zero, result);
        }

        // --- Anti-flicker hysteresis ---

        [Test]
        public void Update_RapidAlternatingFlipsWithinDwellWindow_NeverChangesDirection()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            // Alternate sign every frame for far longer than the dwell window. No single
            // streak is ever sustained, so the direction must never flip - not even once.
            for (int frame = 0; frame < 40; frame++)
            {
                Vector2 velocity = frame % 2 == 0 ? RunLeft : RunRight;
                Vector2 result = _bias.Update(_profile, velocity, FAST, FRAME);

                Assert.AreEqual(
                    MAX_DISTANCE,
                    result.x,
                    0.0001f,
                    $"bias flipped on alternating frame {frame}");
            }
        }

        [Test]
        public void Update_OppositeHeldShorterThanDwell_DoesNotFlip()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            // 0.3s of committed leftward motion against a 0.4s dwell.
            Vector2 result = Run(RunLeft, FAST, 3);

            Assert.AreEqual(MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_OppositeHeldLongerThanDwell_Flips()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            // 0.5s of committed leftward motion against a 0.4s dwell.
            Vector2 result = Run(RunLeft, FAST, 5);

            Assert.AreEqual(-MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_OppositeHeldExactlyDwellTime_Flips()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            // Single frame worth exactly the dwell time, so the boundary is tested
            // without accumulating float error: "at least BiasDwellTime" is inclusive.
            Vector2 result = _bias.Update(_profile, RunLeft, FAST, DWELL_TIME);

            Assert.AreEqual(-MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_OppositeStreakBrokenBeforeDwellElapses_RestartsTheStreak()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            // Three frames left (0.3s, not yet enough), one frame back right, then three
            // frames left again. Neither leftward run reaches the dwell time on its own,
            // and the partial progress must not carry over across the interruption.
            Run(RunLeft, FAST, 3);
            Run(RunRight, FAST, 1);
            Vector2 result = Run(RunLeft, FAST, 3);

            Assert.AreEqual(MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_SlowOppositeDrift_NeverFlipsHoweverLongItLasts()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            // Below the speed gate the samples are not evidence of commitment, so they
            // must not accumulate toward a flip no matter how many arrive.
            Vector2 result = Run(RunLeft, SLOW, 100);

            Assert.AreEqual(MAX_DISTANCE, result.x, 0.0001f);
        }

        // --- Idle hold ---

        [Test]
        public void Update_StationaryAfterRunning_HoldsLastDirectionInsteadOfRecentring()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            Vector2 result = Run(Vector2.zero, 0f, 50);

            // Stopping at the lip of a drop must keep showing what is ahead.
            Assert.AreEqual(MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Update_StationaryThenRunningTheOtherWay_StillNeedsTheFullDwellToFlip()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));
            Run(Vector2.zero, 0f, 50);

            // Idling must not bank dwell credit toward the next flip.
            Vector2 held = Run(RunLeft, FAST, 3);
            Assert.AreEqual(MAX_DISTANCE, held.x, 0.0001f);

            Vector2 flipped = Run(RunLeft, FAST, 2);
            Assert.AreEqual(-MAX_DISTANCE, flipped.x, 0.0001f);
        }

        // --- Reset ---

        [Test]
        public void Reset_ClearsBias_NextUpdateHasNoResidualOffset()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            _bias.Reset();

            // Cinemachine's round-robin standby leaves a non-live vcam arbitrarily
            // stale, so going live must start from nothing rather than snap.
            Assert.AreEqual(Vector2.zero, _bias.Update(_profile, Vector2.zero, 0f, FRAME));
        }

        [Test]
        public void Reset_ClearsDwellDebt_NextQualifyingSampleAdoptsTheNewDirectionImmediately()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            _bias.Reset();

            // Without a full wipe the old rightward sign would survive and fight this
            // sample for a whole dwell window.
            Vector2 result = _bias.Update(_profile, RunLeft, FAST, FRAME);

            Assert.AreEqual(-MAX_DISTANCE, result.x, 0.0001f);
        }

        [Test]
        public void Reset_IsIdempotent()
        {
            Run(RunRight, FAST, FramesFor(DWELL_TIME * 2f));

            _bias.Reset();
            _bias.Reset();

            Assert.AreEqual(Vector2.zero, _bias.Update(_profile, Vector2.zero, 0f, FRAME));
        }

        [Test]
        public void Update_NullProfile_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => _bias.Update(null, RunRight, FAST, FRAME));
        }

        private Vector2 Run(Vector2 velocity, float speedFactor, int frames)
        {
            Vector2 result = Vector2.zero;

            for (int frame = 0; frame < frames; frame++)
            {
                result = _bias.Update(_profile, velocity, speedFactor, FRAME);
            }

            return result;
        }

        private static int FramesFor(float seconds)
        {
            return Mathf.CeilToInt(seconds / FRAME) + 1;
        }
    }
}
