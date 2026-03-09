namespace Game.Components.Health
{
    public enum HealthState
    {
        Normal,
        Invincible
    }

    public class HealthComponent
    {
        public float maxHealth { get; private set; }
        public float currentHealth { get; private set; }
        public HealthState currentState { get; private set; } = HealthState.Normal;

        private float invincibilityTimer;
        private float invincibilityDuration;

        public void Initialize(float max)
        {
            maxHealth = max;
            maxHealth = max;
            currentHealth = maxHealth;
            currentState = HealthState.Normal;
            invincibilityTimer = 0f;
        }


        public void UpdateInvincibility(float deltaTime)
        {
            if (currentState == HealthState.Invincible)
            {
                invincibilityTimer -= deltaTime;
                if (invincibilityTimer <= 0f)
                {
                    invincibilityTimer = 0f;
                    currentState = HealthState.Normal;
                }
            }
        }

        /// <summary>
        /// Attempts to deal damage. Returns true if damage was actually applied.
        /// Damage is blocked while in the Invincible state.
        /// </summary>
        public bool TakeDamage(float amount)
        {
            if (currentState == HealthState.Invincible)
                return false;

            currentHealth -= amount;
            if (currentHealth < 0f)
                currentHealth = 0f;

            return true;
        }

        /// <summary>
        /// Starts the invincibility period for the given duration (in seconds).
        /// </summary>
        public void StartInvincibility(float duration)
        {
            invincibilityDuration = duration;
            invincibilityTimer = duration;
            currentState = HealthState.Invincible;
        }

        public bool IsInvincible()
        {
            return currentState == HealthState.Invincible;
        }

        public void Heal(float amount)
        {
            currentHealth += amount;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;
        }

        public float GetHealthPercentage()
        {
            if (maxHealth <= 0f)
                return 0f;
            return currentHealth / maxHealth;
        }

        public bool IsDead()
        {
            return currentHealth <= 0f;
        }
    }
}
