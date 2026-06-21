using Game.Components.Skills;

namespace Game.Tutorial
{
    /// <summary>
    /// Completes when the player successfully casts the chosen skill. Detection uses the skill
    /// event bus (<see cref="IPlayerSkillEvents"/>), which only fires on a successful activation
    /// (enough energy + off cooldown) — so the step naturally waits until the player has picked
    /// up enough EnergyPickups.
    /// </summary>
    public class CastSkillStep : TutorialStep
    {
        public enum SkillKind { Emp, TrueDamage }

        [UnityEngine.SerializeField] private SkillKind skill = SkillKind.Emp;

        protected override void OnBegin()
        {
            var ev = Context?.SkillEvents;
            if (ev == null) return;

            if (skill == SkillKind.Emp) ev.EmpDetonated += OnEmpDetonated;
            else                        ev.TrueDamageArmed += OnTrueDamageArmed;
        }

        private void OnEmpDetonated(EmpBlastEvent e) => Complete();
        private void OnTrueDamageArmed() => Complete();

        protected override void OnCleanup()
        {
            var ev = Context?.SkillEvents;
            if (ev == null) return;

            ev.EmpDetonated -= OnEmpDetonated;
            ev.TrueDamageArmed -= OnTrueDamageArmed;
        }
    }
}
