using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Gas attack: boss vents gas from the chest fan, spawning a cloud that covers the entire arena.
// The cloud lingers 8-11s and tick-damages the player; the boss is only "busy" during the short
// release window (~1.5s) and can fire other attacks while the cloud persists.
// Cooldown of 35s starts after the cloud dissipates.
public class BossGasAttack : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Boss_Fan child Transform — the chest fan, used as spawn anchor fallback and for spin VFX.")]
    [SerializeField] private Transform fanAnchor;

    [Tooltip("The arena BoxCollider2D ('Epstein's island Boss land') — drives cloud size and position.")]
    [SerializeField] private Collider2D arenaCollider;

    [Tooltip("Gas cloud prefab: BoxCollider2D (isTrigger) + GasCloudHitbox + optional visual.")]
    [SerializeField] private GameObject gasCloudPrefab;

    // ── Timing ────────────────────────────────────────────────────────────────

    [Header("Timing")]
    [Tooltip("How long the boss is 'busy' venting before the cloud spawns.")]
    [SerializeField] private float releaseDuration = 1.5f;

    [Tooltip("Cloud lifetime lower bound (seconds).")]
    [SerializeField] private float minCloudDuration = 8f;

    [Tooltip("Cloud lifetime upper bound (seconds).")]
    [SerializeField] private float maxCloudDuration = 11f;

    [Tooltip("Seconds after the cloud dissipates before this attack can fire again.")]
    [SerializeField] private float cooldown = 35f;

    // ── Events ────────────────────────────────────────────────────────────────

    [Header("Fan Spin")]
    [Tooltip("Animator speed multiplier while gas is releasing (makes fan spin visibly faster).")]
    [SerializeField] private float fanSpinSpeed = 5f;

    [Header("Events")]
    [Tooltip("Fired at the start of the release — hook up fan SFX here.")]
    public UnityEvent OnGasRelease;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool       isAttacking;
    private float      _nextReadyTime;
    private GameObject _activeCloud;
    private Animator   _fanAnimator;
    private float      _fanNormalSpeed = 1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (fanAnchor != null)
        {
            _fanAnimator = fanAnchor.GetComponent<Animator>();
            if (_fanAnimator != null) _fanNormalSpeed = _fanAnimator.speed;
        }
    }

    // ── Public API (mirrors BossClawAttack / BossHammerAttack) ───────────────

    public bool IsAttacking => isAttacking;

    /// <summary>True when the attack can fire: not currently busy and cooldown has elapsed.</summary>
    public bool CanAttack => !isAttacking && Time.time >= _nextReadyTime;

    /// <summary>Start the gas release. Ignored if the attack is on cooldown or already running.</summary>
    public void TriggerGasAttack()
    {
        if (CanAttack) StartCoroutine(GasRoutine());
    }

    /// <summary>Abort immediately and destroy any active cloud (boss death, stagger, etc.).</summary>
    public void ResetGas()
    {
        StopAllCoroutines();
        isAttacking = false;
        if (_activeCloud != null) Destroy(_activeCloud);
        _activeCloud = null;
        SetFanSpeed(_fanNormalSpeed);
    }

    // ── Attack sequence ───────────────────────────────────────────────────────

    private IEnumerator GasRoutine()
    {
        isAttacking = true;

        // Spin the fan fast during the release window (visual telegraph).
        SetFanSpeed(fanSpinSpeed);
        OnGasRelease?.Invoke();

        // Telegraph / release window — boss is "busy" here.
        yield return new WaitForSeconds(releaseDuration);

        // Fan returns to normal speed once the cloud is out.
        SetFanSpeed(_fanNormalSpeed);

        float cloudDuration = Random.Range(minCloudDuration, maxCloudDuration);

        // Spawn the cloud.
        _activeCloud = SpawnCloud(cloudDuration);

        // Cooldown starts counting from the moment the cloud was spawned;
        // it becomes ready 35s after the cloud would have dissipated.
        _nextReadyTime = Time.time + cloudDuration + cooldown;

        // Release the manager — boss can now use hammer/claw while the cloud lingers.
        isAttacking = false;
        _activeCloud = null; // cloud is self-managing from here
    }

    private void SetFanSpeed(float speed)
    {
        if (_fanAnimator != null) _fanAnimator.speed = speed;
    }

    private GameObject SpawnCloud(float duration)
    {
        if (gasCloudPrefab == null) return null;

        // Position at the arena center if available, otherwise at the fan.
        Vector3 center = arenaCollider != null
            ? new Vector3(arenaCollider.bounds.center.x, arenaCollider.bounds.center.y, 0f)
            : (fanAnchor != null ? fanAnchor.position : transform.position);

        GameObject cloud = Instantiate(gasCloudPrefab, center, Quaternion.identity);

        // Collider size is defined on the prefab — respect the user's manual adjustment, don't override it.
        // Read back the actual bounds after placement to pass the correct dimensions to the VFX.
        var col = cloud.GetComponent<BoxCollider2D>();
        float vfxW = col != null ? col.size.x : 10f;
        float vfxH = col != null ? col.size.y : 5f;

        // Start the gas VFX — originates at the fan and spreads to fill the zone.
        Vector3 fanWorldPos = fanAnchor != null ? fanAnchor.position : center;
        cloud.GetComponent<GasCloudVFX>()?.Init(vfxW, vfxH, duration, fanWorldPos);

        // Init the hitbox so it knows its lifetime and self-destructs.
        cloud.GetComponent<GasCloudHitbox>()?.Init(duration);

        return cloud;
    }
}
