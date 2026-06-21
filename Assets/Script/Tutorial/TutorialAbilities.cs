namespace Game.Tutorial
{
    /// <summary>
    /// Canonical ability keys used by the tutorial gating system. These strings are the single
    /// source of truth shared by Player (gate enforcement), PlayerSkillController, and the
    /// tutorial steps that unlock each ability. Walk/jump are intentionally absent — they are
    /// never gated.
    /// </summary>
    public static class TutorialAbilities
    {
        public const string Dash   = "dash";
        public const string Wall   = "wall";
        public const string Grab   = "grab";
        public const string Skill1 = "skill1";
        public const string Skill2 = "skill2";
    }
}
