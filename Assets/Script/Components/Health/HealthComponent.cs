namespace Game.Components.Health
{
    public class HealthComponent
    {
        public float maxHealth { get; private set; }
        public float currentHealth { get; private set; }

        public void Initialize(float max)
        {
            maxHealth = max;
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {

        }

        public void Heal(float amount)
        {

        }

        public float GetHealthPercentage()
        {
            return 0f;
        }

        public bool IsDead()
        {
            return false;
        }
    }
}
