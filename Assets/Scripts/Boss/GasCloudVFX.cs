using System.Collections;
using UnityEngine;

// Controls the gas cloud visual. All particle appearance (color, opacity, size, texture, material,
// noise, rotation) is configured on the ParticleSystem in the Inspector — this script only drives
// the shape size (Box matching the collider) and the emission lifetime so the cloud stays visible
// for the full damage window then fades out naturally.
public class GasCloudVFX : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("Main looping gas cloud. Configure all visuals in Inspector. loop=true, Play On Awake=false.")]
    [SerializeField] private ParticleSystem gasParticles;

    [Tooltip("Optional one-shot burst at the fan when gas first releases. loop=false, Play On Awake=false.")]
    [SerializeField] private ParticleSystem burstParticles;

    [Header("Fade-in")]
    [Tooltip("Seconds to ramp emission from 0 → full rate at cloud spawn. Keeps the fill-in feeling without fan-spread animation.")]
    [Range(0f, 3f)]
    [SerializeField] private float fadeInDuration = 1f;

    // ─────────────────────────────────────────────────────────────────────────

    public void Init(float width, float height, float duration, Vector3 fanWorldPos)
    {
        StartCoroutine(PlayGas(width, height, duration, fanWorldPos));
    }

    private IEnumerator PlayGas(float width, float height, float duration, Vector3 fanWorldPos)
    {
        if (gasParticles == null) yield break;

        // Optional burst at the fan.
        if (burstParticles != null)
        {
            burstParticles.transform.position = fanWorldPos;
            burstParticles.Play();
        }

        // Size the shape to match the damage collider exactly.
        var shape = gasParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position  = Vector3.zero;
        shape.scale     = new Vector3(width, height, 0.5f);

        float targetRate = gasParticles.emission.rateOverTime.constant;

        // Start at zero emission so the cloud fades in rather than popping.
        var emit = gasParticles.emission;
        emit.rateOverTime = 0f;

        gasParticles.Play();

        // ── Fade-in: ramp emission up to full rate ────────────────────────
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            var e = gasParticles.emission;
            e.rateOverTime = Mathf.Lerp(0f, targetRate, elapsed / fadeInDuration);
            yield return null;
        }

        // ── Hold at full emission for the remainder of the damage window ──
        {
            var e = gasParticles.emission;
            e.rateOverTime = targetRate;
        }
        float holdTime = Mathf.Max(0f, duration - fadeInDuration);
        yield return new WaitForSeconds(holdTime);

        // ── Fade out: stop emitting, let live particles alpha-out ─────────
        gasParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        float maxLifetime = gasParticles.main.startLifetime.constantMax;
        yield return new WaitForSeconds(maxLifetime + 0.5f);

        Destroy(gameObject);
    }
}
