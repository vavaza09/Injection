using Game.Components.Movement;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class DashRefreshGateTests
    {
        private const int Max = 1;

        [Test]
        public void TryConsume_FullDash_ReturnsFalseAndLeavesAvailable()
        {
            var gate = new DashRefreshGate();
            bool result = gate.TryConsume(currentDashes: Max, maxDashes: Max);
            Assert.IsFalse(result);
            Assert.IsTrue(gate.Available);
        }

        [Test]
        public void TryConsume_SpentDash_ReturnsTrueAndConsumesAllowance()
        {
            var gate = new DashRefreshGate();
            bool result = gate.TryConsume(currentDashes: 0, maxDashes: Max);
            Assert.IsTrue(result);
            Assert.IsFalse(gate.Available);
        }

        [Test]
        public void TryConsume_SecondCall_ReturnsFalseUntilRecharge()
        {
            var gate = new DashRefreshGate();
            gate.TryConsume(currentDashes: 0, maxDashes: Max);

            bool second = gate.TryConsume(currentDashes: 0, maxDashes: Max);
            Assert.IsFalse(second);

            gate.Recharge();
            bool afterRecharge = gate.TryConsume(currentDashes: 0, maxDashes: Max);
            Assert.IsTrue(afterRecharge);
        }

        [Test]
        public void TryConsume_WhenDisabled_AlwaysReturnsFalse()
        {
            var gate = new DashRefreshGate { Enabled = false };
            bool result = gate.TryConsume(currentDashes: 0, maxDashes: Max);
            Assert.IsFalse(result);
            Assert.IsTrue(gate.Available, "Allowance should not be consumed when gate is disabled");
        }

        [Test]
        public void Recharge_RestoresAvailabilityAfterConsume()
        {
            var gate = new DashRefreshGate();
            gate.TryConsume(currentDashes: 0, maxDashes: Max);
            Assert.IsFalse(gate.Available);

            gate.Recharge();
            Assert.IsTrue(gate.Available);
        }
    }
}
