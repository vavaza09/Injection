using System.Collections;
using UnityEngine;

// Controls the gas cloud visual. All particle appearance (color, opacity, size, texture, material)
// is configured on the ParticleSystem components in the Inspector — this script only drives the
// spread animation: shape starts at the fan position and expands to fill the full collider zone.
//
// Prefab setup: add two child ParticleSystems, assign them below.
//   gasParticles  — looping cloud that fills the zone (loop = true, play on awake = false)
//   burstParticles — one-shot puff at the fan on release (loop = false, play on awake = false) [optional]
public class GasCloudVFX : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("Main looping gas cloud. Configure all visuals here. Must have loop=true, Play On Awake=false.")]
    [SerializeField] private ParticleSystem gasParticles;

    [Tooltip("Optional one-shot burst played at the fan when gas first releases. loop=false, Play On Awake=false.")]
    [SerializeField] private ParticleSystem burstParticles;

    [Header("Spread")]
    [Tooltip("Seconds for the gas to expand from the fan and fill the whole collider zone.")]
    [Range(0.5f, 6f)]
    [SerializeField] private float spreadDuration = 2.5f;

    [Tooltip("Emission rate multiplier at the very start of the spread (0–1). Ramps up to the PS's configured rate.")]
    [Range(0f, 1f)]
    [SerializeField] private float startEmissionFraction = 0.2f;

    // ─────────────────────────────────────────────────────────────────────────

    public void Init(float width, float height, float duration, Vector3 fanWorldPos)
    {
        StartCoroutine(PlayGas(width, height, duration, fanWorldPos));
    }

    private IEnumerator PlayGas(float width, float height, float duration, Vector3 fanWorldPos)
    {
        if (gasParticles == null) yield break;

        // Fan offset relative to the gasParticles transform (which sits at the arena center).
        Vector3 fanLocalOffset = fanWorldPos - gasParticles.transform.position;

        // Read the target emission rate the user configured in the Inspector.
        float targetRate = gasParticles.emission.rateOverTime.constant;

        // ── Optional one-shot burst at the fan ────────────────────────────────
        if (burstParticles != null)
        {
            burstParticles.transform.position = fanWorldPos;
            burstParticles.Play();
        }

        // ── Set gasParticles shape to start at the fan, tiny ─────────────────
        {
            var shape = gasParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position  = fanLocalOffset;
            shape.scale     = new Vector3(0.4f, 0.4f, 0.1f);

            var emit = gasParticles.emission;
            emit.rateOverTime = targetRate * startEmissionFraction;
        }

        gasParticles.Play();

        // ── Spread phase: animate shape from fan to full collider zone ────────
        float elapsed = 0f;
        while (elapsed < spreadDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / spreadDuration);

            var shape = gasParticles.shape;
            shape.position = Vector3.Lerp(fanLocalOffset, Vector3.zero, t);
            shape.scale    = new Vector3(
                Mathf.Lerp(0.4f, width,  t),
                Mathf.Lerp(0.4f, height, t),
                0.1f);

            // Ramp emission rate from the start fraction up to the full configured rate.
            var emit = gasParticles.emission;
            emit.rateOverTime = Mathf.Lerp(targetRate * startEmissionFraction, targetRate, t);

            yield return null;
        }

        // ── Linger at full coverage for remaining duration ────────────────────
        float linger = Mathf.Max(0f, duration - spreadDuration);
        yield return new WaitForSeconds(linger);

        // Stop emitting, let existing particles finish fading.
        gasParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        // Wait for the longest particle lifetime before destroying.
        float maxLifetime = gasParticles.main.startLifetime.constantMax;
        yield return new WaitForSeconds(maxLifetime + 0.5f);

        Destroy(gameObject);
    }
}
