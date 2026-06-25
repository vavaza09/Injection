using NUnit.Framework;
using Game.Rooms.Objectives;

namespace Game.Tests.Rooms
{
    public class KillObjectiveTrackerTests
    {
        [Test]
        public void NotSealed_IsNotComplete()
        {
            var tracker = new KillObjectiveTracker();
            tracker.Register();
            Assert.IsFalse(tracker.IsComplete);
        }

        [Test]
        public void Sealed_NoEnemies_IsNotComplete()
        {
            var tracker = new KillObjectiveTracker();
            tracker.Seal();
            Assert.IsFalse(tracker.IsComplete);
        }

        [Test]
        public void Sealed_EnemiesStillAlive_IsNotComplete()
        {
            var tracker = new KillObjectiveTracker();
            tracker.Register();
            tracker.Register();
            tracker.MarkDead();
            tracker.Seal();
            Assert.IsFalse(tracker.IsComplete);
        }

        [Test]
        public void AllDead_ThenSealed_IsComplete()
        {
            var tracker = new KillObjectiveTracker();
            tracker.Register();
            tracker.Register();
            tracker.MarkDead();
            tracker.MarkDead();
            tracker.Seal();
            Assert.IsTrue(tracker.IsComplete);
        }

        [Test]
        public void Sealed_ThenAllDead_IsComplete()
        {
            var tracker = new KillObjectiveTracker();
            tracker.Register();
            tracker.Seal();
            tracker.MarkDead();
            Assert.IsTrue(tracker.IsComplete);
        }

        [Test]
        public void MarkDead_BeyondRegistered_DoesNotUnderflow()
        {
            var tracker = new KillObjectiveTracker();
            tracker.Register();
            tracker.MarkDead();
            tracker.MarkDead(); // extra call — should not cause negative alive
            tracker.Seal();
            Assert.IsTrue(tracker.IsComplete);
        }
    }
}
