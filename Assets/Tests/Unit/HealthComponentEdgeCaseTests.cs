using Game.Components.Health;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class HealthComponentEdgeCaseTests
    {
        [Test]
        public void Initialize_WithZeroOrNegativeMax_SetsMinimumOfOneHit()
        {
            var sut = new HealthComponent();

            sut.Initialize(0f);

            Assert.AreEqual(1f, sut.maxHealth);
            Assert.AreEqual(1f, sut.currentHealth);
        }

        [Test]
        public void Initialize_WithNegativeValue_SetsMinimumOfOneHit()
        {
            var sut = new HealthComponent();

            sut.Initialize(-5f);

            Assert.AreEqual(1f, sut.maxHealth);
        }

        [Test]
        public void Initialize_WithFractionalValue_CeilsToNextWhole()
        {
            var sut = new HealthComponent();

            sut.Initialize(2.1f);

            Assert.AreEqual(3f, sut.maxHealth);
        }

        [Test]
        public void TakeDamage_WhenAlreadyDead_ReturnsFalse()
        {
            var sut = new HealthComponent();
            sut.Initialize(1f);
            sut.TakeDamage(1f);

            bool applied = sut.TakeDamage(1f);

            Assert.IsFalse(applied);
            Assert.AreEqual(1, sut.GetHitCount());
        }

        [Test]
        public void GetHealthPercentage_WhenFull_ReturnsOne()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);

            Assert.AreEqual(1f, sut.GetHealthPercentage(), 0.001f);
        }

        [Test]
        public void GetHealthPercentage_WhenHalfDepleted_ReturnsHalf()
        {
            var sut = new HealthComponent();
            sut.Initialize(2f);
            sut.TakeDamage(1f);

            Assert.AreEqual(0.5f, sut.GetHealthPercentage(), 0.001f);
        }

        [Test]
        public void GetHealthPercentage_WhenUninitialized_ReturnsZero()
        {
            var sut = new HealthComponent();

            Assert.AreEqual(0f, sut.GetHealthPercentage(), 0.001f);
        }

        [Test]
        public void Heal_WithZeroAmount_DoesNotChangeHealth()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);
            sut.TakeDamage(1f);
            float healthBefore = sut.currentHealth;

            sut.Heal(0f);

            Assert.AreEqual(healthBefore, sut.currentHealth);
        }

        [Test]
        public void Heal_WithNegativeAmount_DoesNotChangeHealth()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);
            sut.TakeDamage(1f);
            float healthBefore = sut.currentHealth;

            sut.Heal(-5f);

            Assert.AreEqual(healthBefore, sut.currentHealth);
        }

        [Test]
        public void Heal_WhenAlreadyAtMax_DoesNotExceedMax()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);

            sut.Heal(10f);

            Assert.AreEqual(3f, sut.currentHealth);
        }

        [Test]
        public void UpdateInvincibility_WithMultipleSteps_ExpiresProperly()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);
            sut.StartInvincibility(0.5f);

            sut.UpdateInvincibility(0.2f);
            Assert.IsTrue(sut.IsInvincible(), "Should still be invincible after partial step");

            sut.UpdateInvincibility(0.3f);
            Assert.IsFalse(sut.IsInvincible(), "Should expire after full duration elapsed");
        }

        [Test]
        public void IsDead_BeforeAnyDamage_ReturnsFalse()
        {
            var sut = new HealthComponent();
            sut.Initialize(3f);

            Assert.IsFalse(sut.IsDead());
        }
    }
}
