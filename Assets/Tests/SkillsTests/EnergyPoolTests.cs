using Game.Components.Skills;
using NUnit.Framework;

namespace Game.Skills.EditModeTests
{
    public class EnergyPoolTests
    {
        [Test]
        public void Constructor_SetsCurrentToStart()
        {
            var pool = new EnergyPool(3, 2);
            Assert.AreEqual(2, pool.Current);
            Assert.AreEqual(3, pool.Max);
        }

        [Test]
        public void Constructor_ClampsStartToMax()
        {
            var pool = new EnergyPool(3, 10);
            Assert.AreEqual(3, pool.Current);
        }

        [Test]
        public void TryAdd_IncreasesCurrentAndReturnsTrue()
        {
            var pool = new EnergyPool(3, 0);
            bool result = pool.TryAdd(1);
            Assert.IsTrue(result);
            Assert.AreEqual(1, pool.Current);
        }

        [Test]
        public void TryAdd_WhenFull_ReturnsFalse()
        {
            var pool = new EnergyPool(3, 3);
            bool result = pool.TryAdd(1);
            Assert.IsFalse(result);
            Assert.AreEqual(3, pool.Current);
        }

        [Test]
        public void TryAdd_CapsAtMax()
        {
            var pool = new EnergyPool(3, 2);
            pool.TryAdd(5);
            Assert.AreEqual(3, pool.Current);
        }

        [Test]
        public void IsFull_TrueWhenAtMax()
        {
            var pool = new EnergyPool(3, 3);
            Assert.IsTrue(pool.IsFull);
        }

        [Test]
        public void IsFull_FalseWhenBelowMax()
        {
            var pool = new EnergyPool(3, 2);
            Assert.IsFalse(pool.IsFull);
        }

        [Test]
        public void CanSpend_TrueWhenEnoughEnergy()
        {
            var pool = new EnergyPool(3, 2);
            Assert.IsTrue(pool.CanSpend(1f));
            Assert.IsTrue(pool.CanSpend(2f));
        }

        [Test]
        public void CanSpend_FalseWhenInsufficientEnergy()
        {
            var pool = new EnergyPool(3, 1);
            Assert.IsFalse(pool.CanSpend(2f));
        }

        [Test]
        public void TrySpend_DecreasesCurrentAndReturnsTrue()
        {
            var pool = new EnergyPool(3, 2);
            bool result = pool.TrySpend(1f);
            Assert.IsTrue(result);
            Assert.AreEqual(1, pool.Current);
        }

        [Test]
        public void TrySpend_WhenInsufficientEnergy_ReturnsFalse()
        {
            var pool = new EnergyPool(3, 0);
            bool result = pool.TrySpend(1f);
            Assert.IsFalse(result);
            Assert.AreEqual(0, pool.Current);
        }

        [Test]
        public void Changed_FiredOnTryAdd()
        {
            var pool = new EnergyPool(3, 0);
            int fired = 0;
            pool.Changed += () => fired++;
            pool.TryAdd(1);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Changed_FiredOnTrySpend()
        {
            var pool = new EnergyPool(3, 2);
            int fired = 0;
            pool.Changed += () => fired++;
            pool.TrySpend(1f);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Changed_NotFiredWhenTryAddFails()
        {
            var pool = new EnergyPool(3, 3);
            int fired = 0;
            pool.Changed += () => fired++;
            pool.TryAdd(1);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void Changed_NotFiredWhenTrySpendFails()
        {
            var pool = new EnergyPool(3, 0);
            int fired = 0;
            pool.Changed += () => fired++;
            pool.TrySpend(1f);
            Assert.AreEqual(0, fired);
        }
    }
}
