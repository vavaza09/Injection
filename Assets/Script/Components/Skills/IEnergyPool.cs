namespace Game.Components.Skills
{
    public interface IEnergyPool
    {
        bool CanSpend(float amount);
        bool TrySpend(float amount);
    }

    public interface IEnergyStore : IEnergyPool
    {
        int Current { get; }
        int Max { get; }
        bool IsFull { get; }
        bool TryAdd(int amount);
        void SetCurrent(int value);
        event System.Action Changed;
    }

    public sealed class InfiniteEnergyPool : IEnergyPool
    {
        public bool CanSpend(float amount) => true;
        public bool TrySpend(float amount) => true;
    }
}
