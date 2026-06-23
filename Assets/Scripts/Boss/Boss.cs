using UnityEngine;
using UnityEngine.U2D.IK;

public class Boss : BossBase
{
    [Header("IK Solver (auto-wires Hammer_Left_Target at Start)")]
    [Tooltip("Drag Boss_Arm_Left_LimbSolver2D here. At Start it receives the hammer target automatically.")]
    [SerializeField] private LimbSolver2D hammerLimbSolver;

    [Header("IK Targets (set by CrabHammerSlam or manually)")]
    [Tooltip("Hammer_Left_Target — the LimbSolver2D effector target.")]
    [SerializeField] private Transform hammerLeftTarget;

    private BossIdleState _idleState;

    private void Start()
    {
        ResolveReferences();
        _idleState = new BossIdleState(this);
        TransitionTo(_idleState);
    }

    public override void OnPlayerDetected()
    {
        // Attack is handled by the CrabHammerSlam component (self-managed via Update).
        Debug.Log("[Boss] Player detected.");
    }

    public override void OnPlayerLost()
    {
        TransitionTo(_idleState);
        Debug.Log("[Boss] Player lost → IdleState");
    }

    public override void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth  = Mathf.Max(0, currentHealth);
        RaiseHealthChanged();
        if (currentHealth <= 0) HandleDeath();
    }

    public void Exit()
    {
        currentState?.Exit();
        StopAllCoroutines();
    }

    private void HandleDeath()
    {
        GetComponent<BossHammerSwing>()?.ResetArm();
        GetComponent<BossGasAttack>()?.ResetGas();
        GetComponent<BossPersistence>()?.MarkDefeated();
        Exit();
        Destroy(gameObject, 1f);
    }

    private void ResolveReferences()
    {
        if (hammerLeftTarget == null) hammerLeftTarget = FindDeep("Hammer_Left_Target");

        // Wire the IK target to the solver so the arm tracks it
        if (hammerLimbSolver != null && hammerLeftTarget != null)
        {
            var chain = hammerLimbSolver.GetChain(0);
            if (chain != null && chain.target == null)
                chain.target = hammerLeftTarget;
        }
    }

    private Transform FindDeep(string childName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.07f);
        Gizmos.DrawSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
