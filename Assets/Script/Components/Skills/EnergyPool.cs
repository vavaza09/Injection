using UnityEngine;

namespace Game.Components.Skills
{
    public sealed class EnergyPool : IEnergyStore
    {
        public int Current { get; private set; }
        public int Max { get; }
        public bool IsFull => Current >= Max;

        public event System.Action Changed;

        public EnergyPool(int max, int start)
        {
            Max     = max;
            Current = Mathf.Clamp(start, 0, max);
        }

        public bool TryAdd(int amount)
        {
            if (IsFull || amount <= 0) return false;
            Current = Mathf.Min(Current + amount, Max);
            Changed?.Invoke();
            return true;
        }

        public bool CanSpend(float amount)
        {
            return Current >= Mathf.CeilToInt(amount);
        }

        public bool TrySpend(float amount)
        {
            int cost = Mathf.CeilToInt(amount);
            if (Current < cost) return false;
            Current -= cost;
            Changed?.Invoke();
            return true;
        }

        public void SetCurrent(int value)
        {
            Current = Mathf.Clamp(value, 0, Max);
            Changed?.Invoke();
        }
    }
}
