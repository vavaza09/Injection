using UnityEngine;
using UnityEngine.XR;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyState currentState = EnemyState.Idle;
    private float stateTimer;

    private void Awake()
    {
        if(enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }
        
        stateTimer = 0f;
    }

    private void Update()
    {
        UpdateAI();
    }

    public void UpdateAI()
    {
        if (enemy == null)
        {
            return;
        }

        switch(currentState)
        {
            case EnemyState.Idle:
                Idle();
                break;
            case EnemyState.Patrol:
                Patrol();
                break;
            case EnemyState.Chase:
                Chase();
                break;
            case EnemyState.Attack:
                Attack();
                break;
            case EnemyState.Dead:
                break;
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (newState == currentState)
        {
            return;
        }

        currentState = newState;
        stateTimer = 0f;

        enemy.SetState(newState);

    }


    private void Idle()
    {
        enemy.DetectPlayer();

        if (enemy.GetState() != EnemyState.Idle)
        {
            ChangeState(enemy.GetState());
            return;
        }

        stateTimer += Time.deltaTime;

        if (stateTimer >= 2f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    private void Patrol()
    {
        enemy.PatrolArea();
        enemy.DetectPlayer();

        if (enemy.GetState() != EnemyState.Patrol)
        {
            ChangeState(enemy.GetState());
            return;
        }
        stateTimer += Time.deltaTime;

        if (stateTimer >= 3f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    private void Chase()
    {
        enemy.ChasePlayer();
        enemy.DetectPlayer();
        if (enemy.GetState() != EnemyState.Chase)
        {
            ChangeState(enemy.GetState());

        }
    }

    private void Attack()
    {
        enemy.DetectPlayer();

        if(enemy.GetState() != EnemyState.Attack)
        {
            ChangeState(enemy.GetState());
            return;
        }

        stateTimer += Time.deltaTime;
        if (stateTimer >= 0.6f)
        {
            enemy.Attack();
            stateTimer = 0f;
        }
    }


}
