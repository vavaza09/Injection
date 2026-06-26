using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// Tutorial-only enemy subclass. Use this component on the skill-step dummy prefab
    /// instead of MeleeEnemy or other enemy types.
    ///
    /// In immortal mode (step 8 / EMP) all incoming TakeDamage calls are silently absorbed —
    /// the dummy shows no damage and never dies. Stun from EMP still applies normally because
    /// Enemy.Stun() is a separate code path that does not go through TakeDamage.
    ///
    /// When immortal mode is off (step 9 / TrueDash) the enemy behaves exactly like any
    /// standard Enemy — it can be killed and its GO is destroyed on death as usual.
    /// </summary>
    public class TutorialDummyEnemy : Enemy
    {
        public bool IsImmortal { get; private set; }

        public void SetImmortal(bool immortal) => IsImmortal = immortal;

        public override bool TakeDamage(float damage)
        {
            if (IsImmortal) return false;
            return base.TakeDamage(damage);
        }

        public override bool TakeMultiHit(int hits)
        {
            if (IsImmortal) return false;
            return base.TakeMultiHit(hits);
        }
    }
}
