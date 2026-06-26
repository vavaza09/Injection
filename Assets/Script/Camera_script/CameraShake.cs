using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-independent camera shake via Cinemachine Impulse.
/// Automatically ensures a CinemachineImpulseListener exists on every vcam.
/// Call CameraShake.Shake(intensity) from anywhere — no manual scene setup needed.
/// </summary>
public static class CameraShake
{
    private static CinemachineImpulseSource _source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSource();
        InstallListeners();
    }

    private static void EnsureSource()
    {
        if (_source != null) return;

        var go = new GameObject("CameraShake_ImpulseSource");
        Object.DontDestroyOnLoad(go);
        _source = go.AddComponent<CinemachineImpulseSource>();

        // Configure via local copy of the struct, then assign back
        var def = _source.ImpulseDefinition;
        def.ImpulseChannel  = 1;
        def.ImpulseType     = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        def.ImpulseShape    = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        def.ImpulseDuration = 0.25f;
        _source.ImpulseDefinition = def;
    }

    private static void InstallListeners()
    {
        var vcams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var vcam in vcams)
        {
            if (vcam.GetComponent<CinemachineImpulseListener>() != null) continue;
            var listener = vcam.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1;
            listener.Gain        = 1f;
            listener.Use2DDistance = true;
        }
    }

    public static void Shake(float intensity, float duration = 0.25f)
    {
        if (_source == null)
        {
            EnsureSource();
            InstallListeners();
        }

        // Mutate duration via local copy
        var def = _source.ImpulseDefinition;
        def.ImpulseDuration = Mathf.Max(0.05f, duration);
        _source.ImpulseDefinition = def;

        // Random 2D direction so shakes vary
        Vector2 dir = Random.insideUnitCircle.normalized;
        _source.GenerateImpulse(new Vector3(dir.x, dir.y, 0f) * intensity);
    }
}
