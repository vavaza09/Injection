using UnityEngine;
namespace Game.Components.Movement
{
    public interface IDashHandler
    {
        bool IsDashing { get; }
        bool DashAttacking { get; }
        bool CanDash { get; }
        Vector2 DashDir { get; }
        int CurrentDashes { get; }
        int MaxDashes { get; }

        bool StartDash(Vector2 direction, bool isGrounded, float speedMultiplier = 1f);
        void UpdateTimers();
        bool RefillDash();
        void ResetDash();
        void RechargeForCombo();
        void Initialize(Rigidbody2D rb);
    }
}
