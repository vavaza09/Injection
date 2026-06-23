namespace Game.Rooms.Objectives
{
    public sealed class KillObjectiveTracker
    {
        private int _registered;
        private int _alive;
        private bool _sealed;

        public bool IsComplete => _sealed && _registered > 0 && _alive == 0;

        public void Register()
        {
            _registered++;
            _alive++;
        }

        public void MarkDead()
        {
            if (_alive > 0) _alive--;
        }

        public void Seal()
        {
            _sealed = true;
        }
    }
}
