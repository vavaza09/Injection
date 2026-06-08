using Game.Components.Movement;
using Game.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Unit
{
    public class DashHandlerTests
    {
        private DashSettings DefaultSettings() => new DashSettings
        {
            dashSpeed = TestGameConfig.DASH_SPEED,
            dashTime = TestGameConfig.DASH_DURATION,
            dashCooldown = TestGameConfig.DASH_COOLDOWN,
            maxDashes = TestGameConfig.DASH_MAX_COUNT
        };

        [Test]
        public void Constructor_SetsCurrentDashesToMax()
        {
            var settings = DefaultSettings();
            var sut = new DashHandler(settings);

            Assert.AreEqual(settings.maxDashes, sut.CurrentDashes);
            Assert.AreEqual(settings.maxDashes, sut.MaxDashes);
        }

        [Test]
        public void Constructor_IsDashingIsFalse()
        {
            var sut = new DashHandler(DefaultSettings());

            Assert.IsFalse(sut.IsDashing);
        }

        [Test]
        public void Constructor_DashAttackingIsFalse()
        {
            var sut = new DashHandler(DefaultSettings());

            Assert.IsFalse(sut.DashAttacking);
        }

        [Test]
        public void Constructor_CanDashIsTrueWithDashesAvailable()
        {
            var settings = DefaultSettings();
            var sut = new DashHandler(settings);

            Assert.IsTrue(sut.CanDash);
        }

        [Test]
        public void Constructor_DashDirIsZero()
        {
            var sut = new DashHandler(DefaultSettings());

            Assert.AreEqual(Vector2.zero, sut.DashDir);
        }

        [Test]
        public void Constructor_WithZeroMaxDashes_CanDashIsFalse()
        {
            var settings = DefaultSettings();
            settings.maxDashes = 0;
            var sut = new DashHandler(settings);

            Assert.IsFalse(sut.CanDash);
            Assert.AreEqual(0, sut.CurrentDashes);
        }

        [Test]
        public void FakeDashHandler_StartDash_DecrementsCurrentDashes()
        {
            var fake = new FakeDashHandler { MaxDashes = 2, CurrentDashes = 2 };

            fake.StartDash(Vector2.right, true);

            Assert.AreEqual(1, fake.CurrentDashes);
        }

        [Test]
        public void FakeDashHandler_ResetDash_RestoresCurrentDashesToMax()
        {
            var fake = new FakeDashHandler { MaxDashes = 1, CurrentDashes = 0 };

            fake.ResetDash();

            Assert.AreEqual(1, fake.CurrentDashes);
            Assert.IsFalse(fake.IsDashing);
        }

        [Test]
        public void FakeDashHandler_RefillDash_ReturnsTrue_WhenBelowMax()
        {
            var fake = new FakeDashHandler { MaxDashes = 1, CurrentDashes = 0 };

            bool refilled = fake.RefillDash();

            Assert.IsTrue(refilled);
            Assert.AreEqual(1, fake.CurrentDashes);
        }

        [Test]
        public void FakeDashHandler_RefillDash_ReturnsFalse_WhenAlreadyFull()
        {
            var fake = new FakeDashHandler { MaxDashes = 1, CurrentDashes = 1 };

            bool refilled = fake.RefillDash();

            Assert.IsFalse(refilled);
        }
    }
}
