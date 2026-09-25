namespace Game.Progression
{
    /// <summary>Canonical ids for <c>SaveService.MarkAbilityUnlocked</c>/<c>IsAbilityUnlocked</c>
    /// — permanent player-progression unlocks, distinct from <c>TutorialAbilities</c> (which
    /// gates the tutorial's temporary, per-run ability lock). Single source of truth shared by
    /// every trigger/consumer of a given unlock.</summary>
    public static class AbilityIds
    {
        public const string Glide = "glide";
        public const string TrueDamage = "true_damage";
    }
}
