namespace Game.Components.Skills
{
    public interface ISkill
    {
        bool CanActivate { get; }
        float CooldownRemaining { get; }
        float EnergyCost { get; }
        void Activate();
        void Tick(float deltaTime);
    }
}
