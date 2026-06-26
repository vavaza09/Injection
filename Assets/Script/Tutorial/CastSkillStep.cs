using Game.Components.Skills;
using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// Completes when the player successfully casts the chosen skill.
    ///
    /// EMP   — completes on EmpDetonated (skill fired).
    /// TrueDamage — completes on TrueDamageConsumed (skill armed via F, then dash executed),
    ///             which captures the full "press F → aim → dash" sequence in one event.
    ///
    /// Optional dummy: assign the same TutorialDummyEnemy GO on both consecutive skill steps.
    /// Set makeDummyImmortal on the EMP step so the dummy absorbs hits without dying,
    /// then leave it off on the TrueDash step so the player can kill it normally.
    /// </summary>
    public class CastSkillStep : TutorialStep
    {
        public enum SkillKind { Emp, TrueDamage }

        [SerializeField] private SkillKind skill = SkillKind.Emp;

        [Header("Shared Dummy (optional)")]
        [Tooltip("Assign the same Enemy GO on both skill steps. The dummy starts inactive and is activated when each step begins.")]
        [SerializeField] private Enemy dummy;
        [Tooltip("AI/behaviour to disable while this step is active so the dummy never retaliates.")]
        [SerializeField] private Behaviour aiBehaviour;
        [Tooltip("Make the dummy immortal while this step is active (use on the EMP step). Immortal mode blocks all TakeDamage calls; EMP stun still shows normally.")]
        [SerializeField] private bool makeDummyImmortal;
        [Tooltip("Keep the dummy active after this step completes (use on the EMP step so the dummy persists into the TrueDash step).")]
        [SerializeField] private bool keepDummyActiveOnComplete;

        protected override void OnBegin()
        {
            if (dummy != null)
            {
                dummy.gameObject.SetActive(true);
                if (aiBehaviour != null) aiBehaviour.enabled = false;

                var tutorialDummy = dummy as TutorialDummyEnemy;
                if (tutorialDummy != null) tutorialDummy.SetImmortal(makeDummyImmortal);
            }

            var ev = Context?.SkillEvents;
            if (ev == null) return;

            if (skill == SkillKind.Emp) ev.EmpDetonated       += OnEmpDetonated;
            else                        ev.TrueDamageConsumed  += OnTrueDamageConsumed;
        }

        private void OnEmpDetonated(EmpBlastEvent e) => Complete();
        private void OnTrueDamageConsumed() => Complete();

        protected override void OnCleanup()
        {
            var ev = Context?.SkillEvents;
            if (ev != null)
            {
                ev.EmpDetonated      -= OnEmpDetonated;
                ev.TrueDamageConsumed -= OnTrueDamageConsumed;
            }

            if (dummy != null)
            {
                var tutorialDummy = dummy as TutorialDummyEnemy;
                if (tutorialDummy != null) tutorialDummy.SetImmortal(false);

                if (aiBehaviour != null) aiBehaviour.enabled = true;

                if (!keepDummyActiveOnComplete)
                    dummy.gameObject.SetActive(false);
            }
        }
    }
}
