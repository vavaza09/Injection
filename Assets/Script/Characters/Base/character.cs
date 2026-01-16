using Game.Components.Health;
using Game.Components.Movement;
using UnityEngine;
using VContainer;

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
        
    }
    
    protected virtual void Start()
    {
        currentHealth = maxHealth;

        // Setup components
        if (healthComponent != null)
        {
            healthComponent.Initialize(maxHealth);
        }

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
        movementComponent?.Update();
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
        movementComponent?.OnDrawGizmosSelected();
    }
}

