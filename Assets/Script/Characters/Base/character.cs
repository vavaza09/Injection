using Game.Components.Health;
using Game.Components.Movement;
using Game.Components.Combat;
using UnityEngine;
using VContainer;
using System;

public abstract class character : MonoBehaviour
{
    [Header("Character Stats")]
    [SerializeField] protected string characterName;
    [SerializeField] protected float maxHealth;
    [SerializeField] protected float moveSpeed;
    [SerializeField] protected float attackDamage;

    //State
    protected float currentHealth;
    protected bool isAlive = true;

    //Unity Components
    protected Transform characterTransform;
    protected Rigidbody2D rb;

    //Components
    protected HealthComponent healthComponent;
    [SerializeField] protected MovementComponent movementComponent;
    protected AttackComponent attackComponent;
    private bool isHealthInitialized;

    public event Action<character> Damaged;
    public event Action<character> Died;
    public event Action<character> Healed;
    public event Action InvincibilityStarted;
    public event Action InvincibilityEnded;

    protected virtual void Awake()
    {
        characterTransform = transform;
        rb = GetComponent<Rigidbody2D>();
        movementComponent = GetComponent<MovementComponent>();
    }

    [Inject]
    protected virtual void InitializeComponent( 
        HealthComponent health
        )
    {
        healthComponent = health;
        isHealthInitialized = false;
    }
    
    protected virtual void Start()
    {
        currentHealth = maxHealth;
        EnsureHealthComponent();

        // Setup components
        if (movementComponent != null)
        {
            movementComponent.Initialize(rb, characterTransform);
            movementComponent.SetSpeed(moveSpeed);
        }

        if (attackComponent != null)
        {
            attackComponent.SetDamage(attackDamage);
        }
    }
    
    protected virtual void Update()
    {
        // Update movement component
        movementComponent?.UpdateMovement();

        // Tick invincibility timer
        healthComponent?.UpdateInvincibility(Time.deltaTime);
    }

    public virtual void TakeDamage(float damage)
    {
        EnsureHealthComponent();

        if (!isAlive)
        {
            return;
        }

        bool damageApplied = healthComponent.TakeDamage(damage);
        if (!damageApplied)
        {
            return;
        }

        OnTakeDamage();
        Damaged?.Invoke(this);

        if (healthComponent.IsDead())
        {
            Die();
        }
    }

    public virtual void Die()
    {
        isAlive = false;
        OnDeath();
        Died?.Invoke(this);
    }

    public virtual void Heal(float amount)
    {
        EnsureHealthComponent();
        healthComponent.Heal(amount);
        Healed?.Invoke(this);
    }

    public abstract void Move(Vector2 direction);
    public abstract void Attack();
    
    protected virtual void OnDeath() 
    { 
        Debug.Log($"{characterName} has died.");
    }
    
    protected virtual void OnTakeDamage()
    { 
        Debug.Log($"{characterName} took damage.");
    }
    
    protected virtual void OnDestroy() 
    { 
        Debug.Log($"{characterName} is being destroyed.");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw ground check gizmo
        //movementComponent?.OnDrawGizmosSelected();
    }

    private void EnsureHealthComponent()
    {
        if (healthComponent == null)
        {
            healthComponent = new HealthComponent();
            isHealthInitialized = false;
        }

        if (!isHealthInitialized)
        {
            healthComponent.Initialize(maxHealth);
            currentHealth = maxHealth;
            isHealthInitialized = true;
            healthComponent.OnInvincibilityStarted += () => InvincibilityStarted?.Invoke();
            healthComponent.OnInvincibilityEnded   += () => InvincibilityEnded?.Invoke();
        }
    }
}

