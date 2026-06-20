namespace Game.Components.Movement
{
    public class DashRefreshGate
    {
        private bool _available = true;

        public bool Enabled { get; set; } = true;
        public bool Available => _available;

        public void Recharge() => _available = true;

        // Returns true (and consumes the allowance) only when enabled, not yet used this
        // airtime, AND the dash was actually spent — so wall-jumping at full dash is a no-op.
        public bool TryConsume(int currentDashes, int maxDashes)
        {
            if (!Enabled || !_available) return false;
            if (currentDashes >= maxDashes) return false;
            _available = false;
            return true;
        }
    }
}
