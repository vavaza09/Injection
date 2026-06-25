using System.Collections.Generic;

namespace Game.Rooms.Objectives
{
    public sealed class PhaseThresholdTracker
    {
        private readonly List<float> _thresholds;
        private readonly bool[] _fired;

        public PhaseThresholdTracker(IReadOnlyList<float> thresholds)
        {
            _thresholds = new List<float>(thresholds);
            _fired = new bool[_thresholds.Count];
        }

        // Returns the index of the threshold just crossed, or -1 if none new was crossed.
        // Thresholds fire at most once each, descending as health drops.
        public int CheckPercent(float percent)
        {
            for (int i = 0; i < _thresholds.Count; i++)
            {
                if (!_fired[i] && percent <= _thresholds[i])
                {
                    _fired[i] = true;
                    return i;
                }
            }
            return -1;
        }
    }
}
