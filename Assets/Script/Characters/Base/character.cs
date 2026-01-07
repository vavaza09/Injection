
using Game.Components.Health;
using Game.Components.Movement;
using UnityEngine;

    public abstract class character : MonoBehaviour
    {
        [Header("Character Stats")]
        [SerializeField] protected string characterName;
        [SerializeField] protected float maxHealth;
        [SerializeField] protected float moveSpeed;
        [SerializeField] protected float attackDamage;

        protected float currentHealth;
        protected bool isAlive = true;

        protected Transform transform;
        protected Rigidbody2D rigidbody2D;
        protected HealthComponent healthComponent;
        protected MovementComponent movementComponent;
        protected AttackComponent attackComponent;

        protected virtual void Start()
        {
            currentHealth = maxHealth;
            healthComponent?.Initialize(maxHealth);
        }

        public virtual void TakeDamage(float damage)
        {
            if (!isAlive) return;

            healthComponent.TakeDamage(damage);
            OnTakeDamage();

            if (healthComponent.IsDead())
            {
                Die();
            }
        }

        public virtual void Die()
        {
            isAlive = false;
            OnDeath();
        }

        public abstract void Move(Vector2 direction);
        public abstract void Attack();
        protected abstract void OnDeath();
        protected abstract void OnTakeDamage();

    }

