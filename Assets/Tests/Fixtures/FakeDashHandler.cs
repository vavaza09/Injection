using UnityEngine;
using Game.Components.Movement;

namespace Game.Tests.Fixtures
{
    public class FakeDashHandler : IDashHandler
    {
        public bool IsDashing { get; set; }
        public bool DashAttacking { get; set; }
        public bool CanDash { get; set; } = true;
        public Vector2 DashDir { get; set; }
        public int CurrentDashes { get; set; } = 1;
        public int MaxDashes { get; set; } = 1;

        public int StartDashCallCount { get; private set; }
        public int ResetDashCallCount { get; private set; }
        public bool StartDashResult { get; set; } = true;

        public bool StartDash(Vector2 direction, bool isGrounded, float speedMultiplier = 1f)
        {
            StartDashCallCount++;
            DashDir = direction;
            if (StartDashResult)
            {
                IsDashing = true;
                CurrentDashes--;
            }
            return StartDashResult;
        }

        public void UpdateTimers() { }

        public bool RefillDash()
        {
            if (CurrentDashes < MaxDashes)
            {
                CurrentDashes = MaxDashes;
                return true;
            }
            return false;
        }

        public void ResetDash()
        {
            ResetDashCallCount++;
            IsDashing = false;
            DashAttacking = false;
            CurrentDashes = MaxDashes;
        }

        public void Initialize(Rigidbody2D rb) { }
    }
}
