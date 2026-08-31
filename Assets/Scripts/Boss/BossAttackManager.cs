using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Rooms;

// Randomly selects and fires the boss's attacks. Controls timing, cooldowns, and anti-repeat logic.
//
// How it works:
//   - Polls player distance every Update. When player enters detectionRadius, attack loop starts.
//   - Each iteration: pick a random valid attack → fire it → wait for it to finish → cooldown → repeat.
//   - Anti-repeat: if the same attack fires maxConsecutiveSameAttack times in a row, the next pick
//     is forced to be a DIFFERENT attack.
//
// To add a 3rd attack later:
//   1. Add a [SerializeField] field for the new script type.
//   2. Add a new AttackEntry to the _attacks array inside BuildAttackTable().
//   Done.
//
// Attach to the boss root alongside BossHammerAttack and BossClawAttack.
public class BossAttackManager : MonoBehaviour
{
    // ── Detection ─────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Player must be within this distance for the attack loop to run.")]
    [SerializeField] private float detectionRadius = 6f;
    [Tooltip("Auto-found by 'Player' tag on Start if left empty.")]
    [SerializeField] private Transform playerTransform;

    // ── Attack Settings ───────────────────────────────────────────────────

    [Header("Attack Settings")]
    [Tooltip("Pause between the end of one attack and the start of the next.")]
    [SerializeField] private float globalCooldown = 1.5f;
    [Tooltip("How many times the same attack can fire consecutively before a forced switch.")]
    [SerializeField] private int maxConsecutiveSameAttack = 3;

    // ── Attack References ─────────────────────────────────────────────────

    [Header("Attack References")]
    [SerializeField] private BossHammerAttack    hammerAttack;
    [SerializeField] private BossClawAttack      clawAttack;
    [SerializeField] private BossGasAttack       gasAttack;
    [SerializeField] private BossJunkSmashAttack junkSmashAttack;

    // ── Debug ─────────────────────────────────────────────────────────────

    [Header("Debug — Read Only")]
    [SerializeField] private string lastAttackName;
    [SerializeField] private int    consecutiveCount;
#pragma warning disable CS0414
    [SerializeField] private bool   playerInRange;
#pragma warning restore CS0414

    // ── Internal ──────────────────────────────────────────────────────────

    // Extend this to add a 3rd attack: add a field above and an entry in BuildAttackTable().
    private struct AttackEntry
    {
        public string              name;
        public System.Func<bool>  canAttack;    // is this attack able to fire right now?
        public System.Action       trigger;      // call to start the attack
        public System.Func<bool>  isAttacking;  // is the attack still running?
    }

    // Fires after each individual attack finishes (before global cooldown).
    // BossWeakPointManager subscribes to count completed attacks.
    public event Action AttackCompleted;

    private AttackEntry[] _attacks;
    private bool          _playerInRange;
    private int           _lastAttackIndex = -1;
    private int           _consecutiveCount;
    private Coroutine     _attackCoroutine;
    private bool          _hammerUsed;
    private bool          _clawUsed;
    private IRoomLoader   _roomLoader;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        if (playerTransform == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTransform = go.transform;
        }

        _roomLoader = FindFirstObjectByType<RoomManager>();
        BuildAttackTable();
    }

    private void Update()
    {
        if (playerTransform == null) return;
        if (_roomLoader != null && _roomLoader.IsTransitioning) return;

        // Also consider "in range" when JunkSmash can fire — platforms sit outside the
        // normal detectionRadius, so without this the attack loop never starts while
        // the player is on a platform.
        bool junkReady = junkSmashAttack != null && junkSmashAttack.CanAttack;
        bool inRange   = junkReady ||
                         Vector2.Distance(transform.position, playerTransform.position) <= detectionRadius;

        if (inRange && !_playerInRange)
        {
            _playerInRange = true;
            playerInRange  = true;
            if (_attackCoroutine == null)
                _attackCoroutine = StartCoroutine(AttackLoop());
        }
        else if (!inRange && _playerInRange)
        {
            _playerInRange = false;
            playerInRange  = false;
            // Loop checks _playerInRange each iteration and exits naturally.
        }
    }

    // ── Attack loop ───────────────────────────────────────────────────────

    private IEnumerator AttackLoop()
    {
        while (_playerInRange)
        {
            int index = SelectNextAttack();

            if (index < 0)
            {
                // No attack is currently available — wait briefly and retry.
                yield return new WaitForSeconds(0.3f);
                continue;
            }

            // Update consecutive-same-attack counter.
            if (index == _lastAttackIndex)
                _consecutiveCount++;
            else
                _consecutiveCount = 1;

            _lastAttackIndex = index;
            consecutiveCount = _consecutiveCount; // sync debug field
            lastAttackName   = _attacks[index].name;

            // Fire.
            _attacks[index].trigger();

            // Track which attacks have been used (for gas attack sequencing).
            if (_attacks[index].name == "Hammer") _hammerUsed = true;
            else if (_attacks[index].name == "Claw") _clawUsed = true;

            // Wait one frame for IsAttacking to flip true.
            yield return null;

            // Wait until the attack finishes.
            while (_attacks[index].isAttacking())
                yield return null;

            AttackCompleted?.Invoke();

            // Global cooldown before next attack.
            yield return new WaitForSeconds(globalCooldown);
        }

        _attackCoroutine = null;
    }

    // ── Attack selection ──────────────────────────────────────────────────

    private int SelectNextAttack()
    {
        // Forced priority: if JunkSmash can fire, always pick it immediately.
        // This punishes the player whenever they're on a platform and the cooldown is ready.
        for (int i = 0; i < _attacks.Length; i++)
        {
            if (_attacks[i].name == "JunkSmash" && _attacks[i].canAttack())
                return i;
        }

        // Build a list of attack indices that can currently fire.
        var valid = new List<int>();
        for (int i = 0; i < _attacks.Length; i++)
            if (_attacks[i].canAttack()) valid.Add(i);

        if (valid.Count == 0) return -1;

        // Pick randomly from the valid set.
        int picked = valid[UnityEngine.Random.Range(0, valid.Count)];

        // Anti-repeat: if the same attack was picked and we've hit the limit,
        // force a different one (if any other valid attack exists).
        if (picked == _lastAttackIndex && _consecutiveCount >= maxConsecutiveSameAttack)
        {
            var others = valid.FindAll(i => i != _lastAttackIndex);
            if (others.Count > 0)
                picked = others[UnityEngine.Random.Range(0, others.Count)];
            // If no others are valid, we have to repeat — edge case.
        }

        return picked;
    }

    // ── Attack table ──────────────────────────────────────────────────────

    // TO ADD A 3RD ATTACK: add a SerializeField field above, then add an AttackEntry here.
    private void BuildAttackTable()
    {
        _attacks = new[]
        {
            new AttackEntry
            {
                name        = "Hammer",
                canAttack   = () => hammerAttack != null && !hammerAttack.IsAttacking,
                trigger     = () => hammerAttack?.TriggerAttack(),
                isAttacking = () => hammerAttack != null && hammerAttack.IsAttacking
            },
            new AttackEntry
            {
                name        = "Claw",
                canAttack   = () => clawAttack != null && clawAttack.CanAttack,
                trigger     = () => clawAttack?.TriggerClawAttack(),
                isAttacking = () => clawAttack != null && clawAttack.IsAttacking
            },
            new AttackEntry
            {
                name        = "Gas",
                canAttack   = () => gasAttack != null && gasAttack.CanAttack && _hammerUsed && _clawUsed,
                trigger     = () => gasAttack?.TriggerGasAttack(),
                isAttacking = () => gasAttack != null && gasAttack.IsAttacking
            },
            new AttackEntry
            {
                name        = "JunkSmash",
                canAttack   = () => junkSmashAttack != null && junkSmashAttack.CanAttack,
                trigger     = () => junkSmashAttack?.TriggerJunkSmash(),
                isAttacking = () => junkSmashAttack != null && junkSmashAttack.IsAttacking
            }
            // New attacks go here ↑
        };
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
