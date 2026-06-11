using System;
using UnityEngine;

namespace Game.Components.Skills
{
    public readonly struct EmpBlastEvent
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly float StunDuration;

        public EmpBlastEvent(Vector2 position, float radius, float stunDuration)
        {
            Position     = position;
            Radius       = radius;
            StunDuration = stunDuration;
        }
    }

    public interface IPlayerSkillEvents
    {
        event Action<EmpBlastEvent> EmpDetonated;
        event Action TrueDamageArmed;
        event Action TrueDamageConsumed;
    }

    // Concrete implementation — only PlayerSkillController raises events.
    // Enemy/UI code injects IPlayerSkillEvents and subscribes without knowing the sender.
    public sealed class PlayerSkillEvents : IPlayerSkillEvents
    {
        public event Action<EmpBlastEvent> EmpDetonated;
        public event Action TrueDamageArmed;
        public event Action TrueDamageConsumed;

        internal void RaiseEmpDetonated(EmpBlastEvent e)  => EmpDetonated?.Invoke(e);
        internal void RaiseTrueDamageArmed()               => TrueDamageArmed?.Invoke();
        internal void RaiseTrueDamageConsumed()            => TrueDamageConsumed?.Invoke();
    }
}
