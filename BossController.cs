using UnityEngine;

/// <summary>
/// Drives the boss. Each frame it checks whether the player is within attack
/// range; if so and the current attack is ready, it triggers it. Cooldown is
/// handled inside the BossAttack base class, so this stays simple.
/// Attach to the Boss root object.
/// </summary>
public class BossController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player Transform. If left empty, will try to find by tag 'Player'.")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Detection")]
    [Tooltip("Boss attacks when the player is within this distance.")]
    [SerializeField] private float attackRange = 5f;

    [Header("Attack")]
    [Tooltip("The attack to run. Defaults to the HammerSmashAttack on this object.")]
    [SerializeField] private BossAttack currentAttack;

    private void Awake()
    {
        if (currentAttack == null) currentAttack = GetComponent<BossAttack>();
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p) player = p.transform;
        }
    }

    private void Update()
    {
        if (player == null || currentAttack == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange && currentAttack.CanAttack)
        {
            currentAttack.Execute();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
