using Game.Components.Health;
using NUnit.Framework;

namespace Game.Tests.Health
{
    public class HealthComponentTests
    {
        [Test]
        public void Initialize_SetsHitPoolAndResetsState()
        {
            var sut = new HealthComponent();

            sut.Initialize(3.2f);

            Assert.AreEqual(4f, sut.maxHealth);
            Assert.AreEqual(4f, sut.currentHealth);
            Assert.AreEqual(0, sut.GetHitCount());
            Assert.AreEqual(HealthState.Normal, sut.currentState);
        }

        [Test]
        public void TakeDamage_AlwaysConsumesOneHit()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);

            bool applied = sut.TakeDamage(999f);

            Assert.IsTrue(applied);
            Assert.AreEqual(2f, sut.currentHealth);
            Assert.AreEqual(1, sut.GetHitCount());
            Assert.AreEqual(2, sut.GetRemainingHitCount());
        }

        [Test]
        public void TakeDamage_IsBlockedDuringInvincibility()
        {
            var sut = new HealthComponent();
            sut.Initialize(2f);
            sut.StartInvincibility(0.5f);

            bool applied = sut.TakeDamage(1f);

            Assert.IsFalse(applied);
            Assert.AreEqual(2f, sut.currentHealth);
            Assert.AreEqual(0, sut.GetHitCount());
        }

        [Test]
        public void UpdateInvincibility_ExpiresStateAndAllowsHit()
        {
            var sut = new HealthComponent();
            sut.Initialize(2f);
            sut.StartInvincibility(0.25f);

            sut.UpdateInvincibility(0.3f);
            bool applied = sut.TakeDamage(1f);

            Assert.AreEqual(HealthState.Normal, sut.currentState);
            Assert.IsTrue(applied);
            Assert.AreEqual(1f, sut.currentHealth);
        }

        [Test]
        public void Heal_UsesHitUnitsAndClampsToMax()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);
            sut.TakeDamage(1f);
            sut.TakeDamage(1f);

            sut.Heal(1.2f);

            Assert.AreEqual(3f, sut.currentHealth);
            Assert.AreEqual(2, sut.GetHitCount());
        }

        [Test]
        public void IsDead_ReturnsTrueAfterMaxHitsConsumed()
        {
            var sut = new HealthComponent();
            sut.Initialize(2f);

            sut.TakeDamage(1f);
            sut.TakeDamage(1f);

            Assert.IsTrue(sut.IsDead());
            Assert.AreEqual(0f, sut.currentHealth);
            Assert.AreEqual(2, sut.GetHitCount());
        }

        [Test]
        public void StartInvincibility_ZeroDurationKeepsNormalState()
        {
            var sut = new HealthComponent();
            sut.Initialize(1f);

            sut.StartInvincibility(0f);

            Assert.AreEqual(HealthState.Normal, sut.currentState);
            Assert.IsFalse(sut.IsInvincible());
        }
    }
}
