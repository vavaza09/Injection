namespace Game.Components.Skills
{
    public enum SkillId { Emp, TrueDamage }

    public readonly struct SkillReadout
    {
        public readonly bool  Unlocked;
        public readonly bool  CanCast;
        public readonly float CooldownRemaining;
        public readonly float CooldownDuration;

        public SkillReadout(bool unlocked, bool canCast, float cooldownRemaining, float cooldownDuration)
        {
            Unlocked          = unlocked;
            CanCast           = canCast;
            CooldownRemaining = cooldownRemaining;
            CooldownDuration  = cooldownDuration;
        }
    }

    public interface ISkillReadinessProvider
    {
        SkillReadout Get(SkillId id);
    }
}
