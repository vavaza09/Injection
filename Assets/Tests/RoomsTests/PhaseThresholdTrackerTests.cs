using NUnit.Framework;
using System.Collections.Generic;
using Game.Rooms.Objectives;

namespace Game.Tests.Rooms
{
    public class PhaseThresholdTrackerTests
    {
        [Test]
        public void SingleThreshold_CrossedOnce_ReturnsIndex()
        {
            var tracker = new PhaseThresholdTracker(new List<float> { 0.5f });
            Assert.AreEqual(0, tracker.CheckPercent(0.4f));
        }

        [Test]
        public void SingleThreshold_CrossedTwice_FiresOnlyOnce()
        {
            var tracker = new PhaseThresholdTracker(new List<float> { 0.5f });
            tracker.CheckPercent(0.4f);
            Assert.AreEqual(-1, tracker.CheckPercent(0.3f));
        }

        [Test]
        public void AboveThreshold_DoesNotFire()
        {
            var tracker = new PhaseThresholdTracker(new List<float> { 0.5f });
            Assert.AreEqual(-1, tracker.CheckPercent(0.6f));
        }

        [Test]
        public void HealthBounceUp_ThenBackDown_DoesNotRefire()
        {
            var tracker = new PhaseThresholdTracker(new List<float> { 0.5f });
            tracker.CheckPercent(0.4f); // cross
            tracker.CheckPercent(0.9f); // back above — no re-arm
            Assert.AreEqual(-1, tracker.CheckPercent(0.3f)); // back below — should not fire again
        }

        [Test]
        public void MultipleThresholds_FireInOrder()
        {
            var tracker = new PhaseThresholdTracker(new List<float> { 0.75f, 0.5f, 0.25f });
            Assert.AreEqual(0, tracker.CheckPercent(0.7f));  // crosses 0.75
            Assert.AreEqual(1, tracker.CheckPercent(0.45f)); // crosses 0.5
            Assert.AreEqual(2, tracker.CheckPercent(0.2f));  // crosses 0.25
        }

        [Test]
        public void MultipleThresholds_AllCrossedAtOnce_OnlyFirstFires()
        {
            var tracker = new PhaseThresholdTracker(new List<float> { 0.75f, 0.5f, 0.25f });
            // Jump straight to 0.1 — crosses all three, but CheckPercent returns the first unfired one
            int idx = tracker.CheckPercent(0.1f);
            Assert.AreEqual(0, idx);
            // Next call fires the second
            Assert.AreEqual(1, tracker.CheckPercent(0.1f));
            // Then the third
            Assert.AreEqual(2, tracker.CheckPercent(0.1f));
            // Then nothing
            Assert.AreEqual(-1, tracker.CheckPercent(0.1f));
        }

        [Test]
        public void NoThresholds_AlwaysReturnsMinusOne()
        {
            var tracker = new PhaseThresholdTracker(new List<float>());
            Assert.AreEqual(-1, tracker.CheckPercent(0f));
        }
    }
}
