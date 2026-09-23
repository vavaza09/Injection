using System;
using Game.Components.CameraForesight;
using NUnit.Framework;
using UnityEngine;

namespace Game.Camera.EditModeTests
{
    public class CameraZoomGateTests
    {
        private const float MIN_SPEED_FACTOR = 0.5f;
        private const float DWELL_TIME = 0.3f;

        private const float FRAME = 0.1f;
        private const float ABOVE_GATE = 0.8f;
        private const float BELOW_GATE = 0.1f;

        private CameraZoomGate _gate;
        private CameraProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _gate = new CameraZoomGate();
            _profile = TestCameraProfile.Create(
                zoomMinSpeedFactor: MIN_SPEED_FACTOR,
                zoomDwellTime: DWELL_TIME);
        }

        [TearDown]
        public void TearDown()
        {
            TestCameraProfile.DestroyAll();
        }

        // --- Engaging the gate ---

        [Test]
        public void Update_SustainedAboveGate_ReturnsLiveSpeedFactor()
        {
            float result = Run(ABOVE_GATE, FramesFor(DWELL_TIME * 3f));

            Assert.AreEqual(ABOVE_GATE, result, 0.0001f);
        }

        [Test]
        public void Update_FreshInstance_FirstAboveGateSample_EngagesImmediately()
        {
            // Dwell guards changing an established state, not the first commitment -
            // otherwise zoom would lag behind the player's very first sprint.
            float result = _gate.Update(_profile, ABOVE_GATE, FRAME);

            Assert.AreEqual(ABOVE_GATE, result, 0.0001f);
        }

        [Test]
        public void Update_FreshInstance_FirstBelowGateSample_StaysDisengaged()
        {
            float result = _gate.Update(_profile, BELOW_GATE, FRAME);

            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Update_ExactlyAtGate_CountsAsPassing()
        {
            // >= , not > : the gate is inclusive of its own threshold.
            float result = _gate.Update(_profile, MIN_SPEED_FACTOR, FRAME);

            Assert.AreEqual(MIN_SPEED_FACTOR, result, 0.0001f);
        }

        // --- Receding at rest (the deliberate divergence from CameraDirectionalBias) ---

        [Test]
        public void Update_BelowGateAfterEngaged_RecedesToZeroInsteadOfHoldingLastValue()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 3f));

            // Unlike bias (which holds its last direction at rest), a camera that stays
            // zoomed out while the player stands still would read as broken, not helpful.
            float result = Run(0f, FramesFor(DWELL_TIME * 3f));

            Assert.AreEqual(0f, result, 0.0001f);
        }

        // --- Anti-flicker hysteresis ---

        [Test]
        public void Update_RapidAlternatingAroundGateWithinDwellWindow_NeverChangesEngagement()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            // Alternate above/below the gate every frame for far longer than the dwell
            // window. No single streak is ever sustained, so engagement must never flip.
            for (int frame = 0; frame < 40; frame++)
            {
                float speedFactor = frame % 2 == 0 ? BELOW_GATE : ABOVE_GATE;
                float result = _gate.Update(_profile, speedFactor, FRAME);

                Assert.Greater(result, 0f, $"gate disengaged on alternating frame {frame}");
            }
        }

        [Test]
        public void Update_BelowGateHeldShorterThanDwell_StaysEngaged()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            // 0.2s below the gate against a 0.3s dwell - not long enough to disengage.
            // Still engaged, so the below-gate sample is echoed straight through
            // (continuous tracking) rather than floored to 0 - flooring only happens once
            // actually disengaged.
            float stillEngaged = Run(BELOW_GATE, 2);
            Assert.AreEqual(BELOW_GATE, stillEngaged, 0.0001f,
                "should echo the live value while still engaged, not floor to 0");

            // Confirms it never disengaged: going back above gate is reflected immediately,
            // with no re-adopt delay, because the gate state itself never changed.
            float backAboveGate = _gate.Update(_profile, ABOVE_GATE, FRAME);
            Assert.AreEqual(ABOVE_GATE, backAboveGate, 0.0001f);
        }

        [Test]
        public void Update_BelowGateHeldLongerThanDwell_Disengages()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            // 0.4s below the gate against a 0.3s dwell.
            float result = Run(BELOW_GATE, 4);

            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Update_BelowGateHeldExactlyDwellTime_Disengages()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            // Single frame worth exactly the dwell time: "at least ZoomDwellTime" is inclusive.
            float result = _gate.Update(_profile, BELOW_GATE, DWELL_TIME);

            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Update_AboveGateHeldLongerThanDwellAfterDisengaging_ReEngages()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));
            Run(BELOW_GATE, FramesFor(DWELL_TIME * 2f));

            float result = Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            Assert.AreEqual(ABOVE_GATE, result, 0.0001f);
        }

        [Test]
        public void Update_DisengageStreakBrokenBeforeDwellElapses_RestartsTheStreak()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            // Two frames below gate (0.2s, not yet enough), one frame back above, then two
            // frames below again. Neither below-gate run reaches the dwell time on its own,
            // so the gate must never actually disengage across the whole sequence.
            Run(BELOW_GATE, 2);
            Run(ABOVE_GATE, 1);
            float result = Run(BELOW_GATE, 2);

            // Still engaged (echoing the live below-gate value rather than floored to 0)
            // proves the interruption reset the dwell progress instead of letting it carry
            // over toward a disengage.
            Assert.AreEqual(BELOW_GATE, result, 0.0001f);
        }

        // --- Reset ---

        [Test]
        public void Reset_NextUpdateAdoptsImmediately_RegardlessOfPriorState()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            _gate.Reset();

            // A vcam coming back live from Cinemachine's round-robin standby must not carry
            // over stale engagement and fight the very next sample for a whole dwell window.
            float result = _gate.Update(_profile, BELOW_GATE, FRAME);

            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Reset_IsIdempotent()
        {
            Run(ABOVE_GATE, FramesFor(DWELL_TIME * 2f));

            _gate.Reset();
            _gate.Reset();

            float result = _gate.Update(_profile, ABOVE_GATE, FRAME);

            Assert.AreEqual(ABOVE_GATE, result, 0.0001f);
        }

        [Test]
        public void Update_NullProfile_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => _gate.Update(null, ABOVE_GATE, FRAME));
        }

        private float Run(float speedFactor, int frames)
        {
            float result = 0f;

            for (int frame = 0; frame < frames; frame++)
            {
                result = _gate.Update(_profile, speedFactor, FRAME);
            }

            return result;
        }

        private static int FramesFor(float seconds)
        {
            return Mathf.CeilToInt(seconds / FRAME) + 1;
        }
    }
}
