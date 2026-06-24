using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Junk Smash — platform punish attack. Both arms raise and slam simultaneously,
// triggering a shockwave and raining 5-6 falling junk pieces across the arena.
// Forced-priority by BossAttackManager when the player is on a platform.
public class BossJunkSmashAttack : MonoBehaviour
{
    // ── IK Targets ────────────────────────────────────────────────────────

    [Header("IK Targets")]
    [Tooltip("Boss_Arm_Left_LimbSolver2D_Target")]
    [SerializeField] private Transform hammerLeftIKTarget;
    [Tooltip("Boss_Arm_Right_LimbSolver2D_Target")]
    [SerializeField] private Transform clawRightIKTarget;

    // ── Raise / Slam Timing ───────────────────────────────────────────────

    [Header("Raise / Slam")]
    [SerializeField] private Vector2 raiseOffset  = new Vector2(0f, 5f);
    [SerializeField] private Vector2 slamOffset   = new Vector2(0f, -2f);
    [SerializeField] private float raiseDuration   = 0.7f;
    [SerializeField] private float slamDuration    = 0.25f;
    [SerializeField] private float holdDuration    = 0.35f;
    [SerializeField] private float recoverDuration = 0.6f;
    [SerializeField] private AnimationCurve raiseCurve;
    [SerializeField] private AnimationCurve slamCurve;
    [SerializeField] private AnimationCurve recoverCurve;

    // ── Junk ──────────────────────────────────────────────────────────────

    [Header("Junk")]
    [Tooltip("Junk prefabs — each piece picks one at random. Prefab needs a BossJunk component + SpriteRenderer. Leave empty to use the white-square placeholder fallback.")]
    [SerializeField] private List<GameObject> junkPrefabs;
    [SerializeField] private int   minJunk     = 5;
    [SerializeField] private int   maxJunk     = 6;
    [Tooltip("Y above the slam world position to spawn junk from.")]
    [SerializeField] private float spawnHeight = 10f;
    [Tooltip("Arena bounds collider — assign 'Epstein's island Boss land'. Drives the junk scatter band. Falls back to arenaMinX/Max if not set.")]
    [SerializeField] private Collider2D arenaCollider;
    [Tooltip("Fallback left edge for junk scatter, used only when arenaCollider is not assigned.")]
    [SerializeField] private float arenaMinX   = 88f;
    [Tooltip("Fallback right edge for junk scatter, used only when arenaCollider is not assigned.")]
    [SerializeField] private float arenaMaxX   = 132f;
    [Tooltip("Must include Platform (11) AND Ground (3) so junk lands on whichever surface is below.")]
    [SerializeField] private LayerMask junkGroundMask;
    [SerializeField] private StompImpactVFX junkImpactVfxPrefab;
    [SerializeField] private float junkImpactRadius = 1.2f;
    [SerializeField] private float junkDamage       = 1f;

    // ── Impact Event ──────────────────────────────────────────────────────

    [Header("Impact")]
    [Tooltip("Wire to HammerShockwave.OnHammerImpact() for ripple + knockback + shake.")]
    public UnityEvent OnImpact;

    // ── Cooldown ──────────────────────────────────────────────────────────

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 15f;

    // ── State (public for BossAttackManager) ─────────────────────────────

    public bool IsAttacking { get; private set; }
    public bool CanAttack
    {
        get
        {
            if (_player == null) TryFindPlayer();
            return !IsAttacking && _player != null && _player.IsOnPlatform && Time.time >= _nextReadyTime;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private Vector3 _idleHammerPos;
    private Vector3 _idleClawPos;
    private float   _nextReadyTime;
    private Player  _player;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureDefaultCurves();
    }

    private void Start()
    {
        // Capture idle positions exactly once — CLAUDE.md constraint.
        _idleHammerPos = hammerLeftIKTarget != null ? hammerLeftIKTarget.position : Vector3.zero;
        _idleClawPos   = clawRightIKTarget  != null ? clawRightIKTarget.position  : Vector3.zero;

        TryFindPlayer();

        if (hammerLeftIKTarget == null) Debug.LogWarning("[BossJunkSmashAttack] hammerLeftIKTarget not assigned.");
        if (clawRightIKTarget  == null) Debug.LogWarning("[BossJunkSmashAttack] clawRightIKTarget not assigned.");
        if (_player            == null) Debug.LogWarning("[BossJunkSmashAttack] Player not found in Start — will retry on CanAttack.");
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void TriggerJunkSmash()
    {
        if (!IsAttacking) StartCoroutine(JunkSmashRoutine());
    }

    public void ResetArms()
    {
        StopAllCoroutines();
        IsAttacking = false;
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = _idleHammerPos;
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = _idleClawPos;
    }

    private void TryFindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;
        _player = go.GetComponent<Player>() ?? go.GetComponentInParent<Player>();
    }

    // ── Attack sequence ───────────────────────────────────────────────────

    private IEnumerator JunkSmashRoutine()
    {
        IsAttacking = true;

        // ── Raise ────────────────────────────────────────────────────────
        SoundManager.PlaySound(SoundType.BOSS_HAMMER_WINDUP);

        Vector3 raisedHammerPos = _idleHammerPos + (Vector3)(Vector2)raiseOffset;
        Vector3 raisedClawPos   = _idleClawPos   + (Vector3)(Vector2)raiseOffset;

        float e = 0f;
        while (e < raiseDuration)
        {
            e += Time.deltaTime;
            float t = raiseCurve.Evaluate(Mathf.Clamp01(e / raiseDuration));
            if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = Vector3.Lerp(_idleHammerPos, raisedHammerPos, t);
            if (clawRightIKTarget  != null) clawRightIKTarget.position  = Vector3.Lerp(_idleClawPos,   raisedClawPos,   t);
            yield return null;
        }
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = raisedHammerPos;
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = raisedClawPos;

        // ── Slam ─────────────────────────────────────────────────────────
        SoundManager.PlaySound(SoundType.BOSS_HAMMER_SWING);

        Vector3 slamHammerPos = _idleHammerPos + (Vector3)(Vector2)slamOffset;
        Vector3 slamClawPos   = _idleClawPos   + (Vector3)(Vector2)slamOffset;

        e = 0f;
        while (e < slamDuration)
        {
            e += Time.deltaTime;
            float t = slamCurve.Evaluate(Mathf.Clamp01(e / slamDuration));
            if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = Vector3.Lerp(raisedHammerPos, slamHammerPos, t);
            if (clawRightIKTarget  != null) clawRightIKTarget.position  = Vector3.Lerp(raisedClawPos,   slamClawPos,   t);
            yield return null;
        }
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = slamHammerPos;
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = slamClawPos;

        // ── Impact ────────────────────────────────────────────────────────
        SoundManager.PlaySound(SoundType.BOSS_HAMMER_IMPACT);
        OnImpact?.Invoke();
        SpawnJunkWave(slamHammerPos.y);
        _nextReadyTime = Time.time + cooldown;

        yield return new WaitForSeconds(holdDuration);

        // ── Recover ───────────────────────────────────────────────────────
        SoundManager.PlaySound(SoundType.BOSS_HAMMER_RECOVER);

        e = 0f;
        while (e < recoverDuration)
        {
            e += Time.deltaTime;
            float t = recoverCurve.Evaluate(Mathf.Clamp01(e / recoverDuration));
            if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = Vector3.Lerp(slamHammerPos, _idleHammerPos, t);
            if (clawRightIKTarget  != null) clawRightIKTarget.position  = Vector3.Lerp(slamClawPos,   _idleClawPos,   t);
            yield return null;
        }
        // Snap to exact idle so subsequent hammer/claw attacks start from the right origin.
        if (hammerLeftIKTarget != null) hammerLeftIKTarget.position = _idleHammerPos;
        if (clawRightIKTarget  != null) clawRightIKTarget.position  = _idleClawPos;

        IsAttacking = false;
    }

    // ── Junk spawning ─────────────────────────────────────────────────────

    private void SpawnJunkWave(float slamWorldY)
    {
        int count  = Random.Range(minJunk, maxJunk + 1);
        float spawnY = slamWorldY + spawnHeight;

        // Derive the horizontal scatter band from the arena collider when available.
        float minX = arenaCollider != null ? arenaCollider.bounds.min.x : arenaMinX;
        float maxX = arenaCollider != null ? arenaCollider.bounds.max.x : arenaMaxX;

        float[] xPositions = new float[count];
        for (int i = 0; i < count; i++)
            xPositions[i] = Random.Range(minX, maxX);

        // Force one piece to target the player's current X.
        if (_player != null && count > 0)
            xPositions[Random.Range(0, count)] = _player.transform.position.x;

        foreach (float x in xPositions)
        {
            Vector3 spawnPos = new Vector3(x, spawnY, 0f);
            BossJunk junk;

            if (junkPrefabs != null && junkPrefabs.Count > 0)
            {
                var prefab  = junkPrefabs[Random.Range(0, junkPrefabs.Count)];
                var junkGO  = Instantiate(prefab, spawnPos, Quaternion.identity);
                junk        = junkGO.GetComponent<BossJunk>();
                if (junk == null) junk = junkGO.AddComponent<BossJunk>();
            }
            else
            {
                var junkGO = new GameObject("BossJunk");
                junkGO.transform.position = spawnPos;
                junk = junkGO.AddComponent<BossJunk>();
            }

            junk.Initialize(junkGroundMask, junkImpactVfxPrefab, junkImpactRadius, junkDamage);
        }
    }

    // ── Curves ────────────────────────────────────────────────────────────

    private void EnsureDefaultCurves()
    {
        if (raiseCurve   == null || raiseCurve.length   == 0)
            raiseCurve   = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        if (slamCurve    == null || slamCurve.length    == 0)
            slamCurve    = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f), new Keyframe(1f, 1f, 0f, 0f));
        if (recoverCurve == null || recoverCurve.length == 0)
            recoverCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }

    private void Reset()
    {
        raiseCurve   = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        slamCurve    = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f), new Keyframe(1f, 1f, 0f, 0f));
        recoverCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    }

    private void OnDrawGizmos()
    {
        // Horizontal bar showing the junk scatter band at slam height.
        float minX = arenaCollider != null ? arenaCollider.bounds.min.x : arenaMinX;
        float maxX = arenaCollider != null ? arenaCollider.bounds.max.x : arenaMaxX;
        float barY = transform.position.y + slamOffset.y;

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.7f);
        Gizmos.DrawLine(new Vector3(minX, barY, 0f), new Vector3(maxX, barY, 0f));
        Gizmos.DrawLine(new Vector3(minX, barY - 0.3f, 0f), new Vector3(minX, barY + 0.3f, 0f));
        Gizmos.DrawLine(new Vector3(maxX, barY - 0.3f, 0f), new Vector3(maxX, barY + 0.3f, 0f));
    }
}
