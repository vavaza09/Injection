using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ShockWaveManager : MonoBehaviour
{
    [SerializeField] private float shockWaveTime = 0.75f;
    [SerializeField] private float waveStart = -0.1f;
    [SerializeField] private float waveEnd   = 1.2f;

    private SpriteRenderer _sr;
    private Material       _mat;
    private Coroutine      _activeRoutine;

    private static readonly int WaveDistanceProp = Shader.PropertyToID("_WaveDistanceFromCenter");
    private static readonly int RingSpawnPosProp  = Shader.PropertyToID("_RingSpawnPosition");

    private void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _mat = _sr.material;             // instance material — modifications stay local
        _sr.enabled = false;
        _mat.SetFloat(WaveDistanceProp, waveStart);
    }

    /// <summary>
    /// Trigger the ripple. The ring will expand outward from the screen position
    /// of <paramref name="impactWorldPos"/>.
    /// </summary>
    public void Play(Vector3 impactWorldPos)
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _mat.SetFloat(WaveDistanceProp, waveStart);
        }

        // Convert world impact position to viewport (0..1) for the shader
        Vector3 vp = Camera.main.WorldToViewportPoint(impactWorldPos);
        _mat.SetVector(RingSpawnPosProp, new Vector4(vp.x, vp.y, 0f, 0f));

        _activeRoutine = StartCoroutine(ShockWaveRoutine());
    }

    private IEnumerator ShockWaveRoutine()
    {
        _sr.enabled = true;
        _mat.SetFloat(WaveDistanceProp, waveStart);

        float elapsed = 0f;
        while (elapsed < shockWaveTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shockWaveTime);
            _mat.SetFloat(WaveDistanceProp, Mathf.Lerp(waveStart, waveEnd, t));
            yield return null;
        }

        _mat.SetFloat(WaveDistanceProp, waveStart);
        _sr.enabled = false;
        _activeRoutine = null;
    }
}
