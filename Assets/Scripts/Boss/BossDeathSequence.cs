using System.Collections;
using UnityEngine;

// Triggered by Boss.HandleDeath(). Runs the full 10-second boss death set-piece:
// repeated explosions across body parts + body sinks + claw reaches up + finale + head launch.
public class BossDeathSequence : MonoBehaviour
{
    [Header("Explosion Art")]
    [SerializeField] private Sprite[] explosionFrames;
    [SerializeField] private float explosionFps = 12f;
    [SerializeField] private float explosionScale = 0.6f;
    [SerializeField] private float explosionScaleFinal = 1.6f;

    [Header("Explosion Points (body parts)")]
    [Tooltip("Transforms where explosions can randomly fire during the death sequence.")]
    [SerializeField] private Transform[] explosionPoints;

    [Header("Timing")]
    [SerializeField] private float deathDuration = 10f;
    [SerializeField] private float explosionInterval = 0.35f;

    [Header("Body Sink")]
    [Tooltip("The root transform of the entire boss visual (===Boss===Last). Leave empty to auto-use transform.root.")]
    [SerializeField] private Transform sinkRoot;
    [SerializeField] private float sinkDistance = 3f;

    [Header("Claw (right arm IK target)")]
    [Tooltip("Boss_Arm_Right_LimbSolver2D_Target — the IK end-effector. Moving this lifts the claw.")]
    [SerializeField] private Transform clawIKTarget;
    [SerializeField] private float clawLift = 2.5f;

    [Header("Head Launch")]
    [Tooltip("Boss_Head child transform — hidden at the finale; used only to know where to spawn the dead head.")]
    [SerializeField] private Transform headTransform;
    [Tooltip("Prefab spawned at Boss_Head position when the boss body disappears (Boss_dead_head).")]
    [SerializeField] private GameObject deadHeadPrefab;
    [SerializeField] private float headLaunchForce = 9f;
    [SerializeField] private float headLaunchAngle = 75f;
    [SerializeField] private float headAngularVelocity = 240f;
    [SerializeField] private float headGravityScale = 2.5f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundRayLength = 25f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;

    private bool _playing;

    public void Play()
    {
        if (_playing) return;
        _playing = true;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // ── Setup ─────────────────────────────────────────────────────────────

        if (sfxSource == null) sfxSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        SoundManager.PlaySoundOn(SoundType.BOSS_DEATH, sfxSource);

        var idle = GetComponent<BossIdleAnimation>();
        if (idle != null) idle.enabled = false;

        DisableAttacks();

        if (sinkRoot == null) sinkRoot = transform.root;

        Vector3 sinkStartPos = sinkRoot.position;
        RaycastHit2D groundHit = Physics2D.Raycast(sinkRoot.position, Vector2.down, groundRayLength, groundMask);
        float sinkTargetY = groundHit.collider != null
            ? groundHit.point.y
            : sinkRoot.position.y - sinkDistance;

        Vector3 clawWorldStart = clawIKTarget != null ? clawIKTarget.position : Vector3.zero;

        // ── 10-second loop: explosions + sink + claw lift ─────────────────────

        float timer = 0f;
        float nextExplosion = 0f;

        while (timer < deathDuration)
        {
            float t = timer / deathDuration;
            float eased = t * t * (3f - 2f * t); // smoothstep

            // Sink entire boss root
            Vector3 pos = sinkRoot.position;
            pos.y = Mathf.Lerp(sinkStartPos.y, sinkTargetY, eased);
            sinkRoot.position = pos;

            // Lift the claw's IK target in world space (arm reaches upward for help)
            if (clawIKTarget != null)
            {
                Vector3 clawPos = clawIKTarget.position;
                clawPos.y = clawWorldStart.y + Mathf.Lerp(0f, clawLift, eased);
                clawIKTarget.position = clawPos;
            }

            // Spawn explosion at random body part
            if (timer >= nextExplosion && explosionPoints != null && explosionPoints.Length > 0)
            {
                Transform pt = explosionPoints[Random.Range(0, explosionPoints.Length)];
                if (pt != null)
                    StartCoroutine(PlayExplosionAt(pt.position, explosionScale));
                nextExplosion = timer + explosionInterval;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // ── Finale ────────────────────────────────────────────────────────────

        // Capture head world position before we hide everything
        Vector3 headWorldPos = headTransform != null ? headTransform.position : sinkRoot.position;

        // Hide all visual sprites on the boss (including Boss_Head)
        foreach (var sr in sinkRoot.GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        // Final large explosion — await it so it fully plays before we destroy the root
        yield return StartCoroutine(PlayExplosionAt(sinkRoot.position, explosionScaleFinal));

        // Spawn Boss_dead_head prefab at the rig head position and launch it
        // (it is unparented so it survives boss root destruction)
        if (deadHeadPrefab != null)
        {
            var deadHead = Instantiate(deadHeadPrefab, headWorldPos, Quaternion.identity);
            LaunchHead(deadHead.transform);
        }

        yield return new WaitForSeconds(0.3f);

        // Destroy the entire boss hierarchy (BossDeathSequence dies with it — that's fine, we're done)
        Destroy(sinkRoot.gameObject);
    }

    // ── Attack disable ────────────────────────────────────────────────────────

    private void DisableAttacks()
    {
        StopAndDisable<BossAttackManager>();
        StopAndDisable<BossHammerAttack>();
        StopAndDisable<BossClawAttack>();
        StopAndDisable<BossGasAttack>();
        StopAndDisable<BossJunkSmashAttack>();
        StopAndDisable<BossHammerSwing>();
    }

    private void StopAndDisable<T>() where T : MonoBehaviour
    {
        var c = GetComponent<T>();
        if (c == null) return;
        c.StopAllCoroutines();
        c.enabled = false;
    }

    // ── Head launch ───────────────────────────────────────────────────────────

    private void LaunchHead(Transform head)
    {
        var sr = head.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        var rb = head.GetComponent<Rigidbody2D>() ?? head.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = headGravityScale;
        rb.freezeRotation = false;

        if (head.GetComponent<Collider2D>() == null)
        {
            var col = head.gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
        }

        float angleRad = headLaunchAngle * Mathf.Deg2Rad;
        rb.linearVelocity = new Vector2(Mathf.Cos(angleRad) * headLaunchForce, Mathf.Sin(angleRad) * headLaunchForce);
        rb.angularVelocity = headAngularVelocity;

        var lander = head.gameObject.AddComponent<BossHeadLanding>();
        lander.Initialize(rb, groundMask);
    }

    // ── Explosion FX ──────────────────────────────────────────────────────────

    private IEnumerator PlayExplosionAt(Vector3 worldPos, float scale)
    {
        if (explosionFrames == null || explosionFrames.Length == 0) yield break;

        var go = new GameObject("_BossDeathFX");
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 10;

        float frameDt = 1f / Mathf.Max(explosionFps, 1f);
        foreach (var frame in explosionFrames)
        {
            if (go == null) yield break;
            sr.sprite = frame;
            yield return new WaitForSeconds(frameDt);
        }

        if (go != null) Destroy(go);
    }
}

// Attached at runtime to the detached Boss_Head. Waits for it to touch ground then freezes it.
public class BossHeadLanding : MonoBehaviour
{
    private Rigidbody2D _rb;
    private LayerMask _groundMask;

    public void Initialize(Rigidbody2D rb, LayerMask groundMask)
    {
        _rb = rb;
        _groundMask = groundMask;
        StartCoroutine(WatchLanding());
    }

    private IEnumerator WatchLanding()
    {
        yield return new WaitForSeconds(0.4f);

        while (_rb != null)
        {
            if (_rb.linearVelocity.y <= 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.6f, _groundMask);
                if (hit.collider != null)
                {
                    _rb.bodyType = RigidbodyType2D.Static;
                    Destroy(this);
                    yield break;
                }
            }
            yield return null;
        }
    }
}
