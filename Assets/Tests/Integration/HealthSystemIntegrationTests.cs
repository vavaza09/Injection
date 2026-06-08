using Game.Components.Health;
using Game.Tests.Fixtures;
using NUnit.Framework;

namespace Game.Tests.Integration
{
    /// <summary>
    /// Tests the full damage → invincibility → expiry → heal lifecycle
    /// as it would be driven by Player.cs in the actual game.
    /// </summary>
    public class HealthSystemIntegrationTests
    {
        private HealthComponent _health;

        [SetUp]
        public void Setup()
        {
            _health = new HealthComponent();
            _health.Initialize(TestGameConfig.PLAYER_HEALTH_HITS);
        }

        [Test]
        public void DamageFlow_TakesHit_StartsInvincibility_BlocksNextHit()
        {
            bool firstHit = _health.TakeDamage(1f);
            _health.StartInvincibility(TestGameConfig.PLAYER_INVINCIBILITY_DURATION);

            bool secondHit = _health.TakeDamage(1f);

            Assert.IsTrue(firstHit, "First hit should land");
            Assert.IsFalse(secondHit, "Second hit should be blocked by invincibility");
            Assert.AreEqual(TestGameConfig.PLAYER_HEALTH_HITS - 1f, _health.currentHealth);
        }

        [Test]
        public void DamageFlow_InvincibilityExpires_AllowsNextHit()
        {
            _health.TakeDamage(1f);
            _health.StartInvincibility(TestGameConfig.PLAYER_INVINCIBILITY_DURATION);

            _health.UpdateInvincibility(TestGameConfig.PLAYER_INVINCIBILITY_DURATION + 0.1f);
            bool hitAfterExpiry = _health.TakeDamage(1f);

            Assert.IsTrue(hitAfterExpiry);
            Assert.AreEqual(TestGameConfig.PLAYER_HEALTH_HITS - 2f, _health.currentHealth);
        }

        [Test]
        public void FullLifecycle_DamageToDeathToRevive()
        {
            for (int i = 0; i < (int)TestGameConfig.PLAYER_HEALTH_HITS; i++)
            {
                _health.TakeDamage(1f);
            }

            Assert.IsTrue(_health.IsDead());

            _health.Initialize(TestGameConfig.PLAYER_HEALTH_HITS);

            Assert.IsFalse(_health.IsDead());
            Assert.AreEqual(TestGameConfig.PLAYER_HEALTH_HITS, _health.currentHealth);
            Assert.AreEqual(0, _health.GetHitCount());
        }

        [Test]
        public void HealAfterDamage_PartialHeal_DoesNotExceedMax()
        {
            _health.TakeDamage(1f);
            _health.TakeDamage(1f);

            _health.Heal(10f);

            Assert.AreEqual(TestGameConfig.PLAYER_HEALTH_HITS, _health.currentHealth);
            Assert.IsFalse(_health.IsDead());
        }

        [Test]
        public void SimulatedFrameUpdate_InvincibilityTicksDownCorrectly()
        {
            _health.TakeDamage(1f);
            _health.StartInvincibility(0.1f);

            int frames = 0;
            while (_health.IsInvincible() && frames < 1000)
            {
                _health.UpdateInvincibility(TestGameConfig.DELTA_TIME_STEP);
                frames++;
            }

            Assert.IsFalse(_health.IsInvincible(), "Invincibility should have expired");
            Assert.Less(frames, 1000, "Should expire within a reasonable number of frames");
        }

        [Test]
        public void FakeDashHandler_TracksDashUsageAcrossReset()
        {
            var dash = new FakeDashHandler();
            dash.StartDash(UnityEngine.Vector2.right, true);
            dash.StartDash(UnityEngine.Vector2.up, false);

            Assert.AreEqual(2, dash.StartDashCallCount);

            dash.ResetDash();

            Assert.AreEqual(1, dash.ResetDashCallCount);
            Assert.AreEqual(dash.MaxDashes, dash.CurrentDashes);
            Assert.IsFalse(dash.IsDashing);
        }
    }
}
