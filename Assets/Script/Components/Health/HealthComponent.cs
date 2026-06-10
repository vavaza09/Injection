using System;

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
        public int hitCount { get; private set; }
        public HealthState currentState { get; private set; } = HealthState.Normal;

        public event Action OnInvincibilityStarted;
        public event Action OnInvincibilityEnded;

        private float invincibilityTimer;

        public void Initialize(float max)
        {
            int maxHits = max <= 0f ? 1 : (int)System.Math.Ceiling(max);
            maxHealth = maxHits;
            currentHealth = maxHealth;
            hitCount = 0;
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
                    OnInvincibilityEnded?.Invoke();
                }
            }
        }

        /// <summary>
        /// Attempts to deal damage. Returns true if damage was actually applied.
        /// Damage is blocked while in the Invincible state.
        /// </summary>
        public bool TakeDamage(float amount)
        {
            _ = amount; // Kept for API compatibility; each hit always counts as one.

            if (currentState == HealthState.Invincible || IsDead())
                return false;

            hitCount++;
            currentHealth -= 1f;
            if (currentHealth < 0f)
                currentHealth = 0f;

            return true;
        }

        /// <summary>
        /// Starts the invincibility period for the given duration (in seconds).
        /// </summary>
        public void StartInvincibility(float duration)
        {
            if (duration <= 0f)
            {
                invincibilityTimer = 0f;
                currentState = HealthState.Normal;
                return;
            }

            invincibilityTimer = duration;
            currentState = HealthState.Invincible;
            OnInvincibilityStarted?.Invoke();
        }

        public bool IsInvincible()
        {
            return currentState == HealthState.Invincible;
        }

        public void Heal(float amount)
        {
            int healHits = amount <= 0f ? 0 : (int)System.Math.Ceiling(amount);
            if (healHits <= 0)
                return;

            currentHealth += healHits;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;
        }

        public int GetHitCount()
        {
            return hitCount;
        }

        public int GetRemainingHitCount()
        {
            return (int)currentHealth;
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
