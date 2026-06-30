using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerDamageFeedback : MonoBehaviour
{
    [Header("Screen Shake")]
    [SerializeField] private float shakeIntensity = 0.4f;
    [SerializeField] private float shakeDuration   = 0.25f;

    [Header("Audio Muffle")]
    [SerializeField] private float muffledCutoff  = 700f;
    [SerializeField] private float normalCutoff   = 22000f;
    [SerializeField] private float audioFadeIn    = 0.08f;
    [SerializeField] private float audioFadeOut   = 0.3f;

    [Header("Volume Dip")]
    [SerializeField, Range(0f, 1f)] private float duckMultiplier = 0.35f;

    [Header("Vignette")]
    [SerializeField] private float vignettePeak    = 0.55f;
    [SerializeField] private float vignetteFadeIn  = 0.08f;
    [SerializeField] private float vignetteFadeOut = 0.4f;
    [SerializeField] private Volume volumeOverride;

    private AudioLowPassFilter _lowPass;
    private Vignette           _vignette;
    private float              _vignetteBase = 0.2f;
    private bool               _vignetteReady;

    private Coroutine _feedbackRoutine;

    private void Start()
    {
        EnsureLowPass();
        EnsureVignette();
    }

    private void EnsureLowPass()
    {
        var listener = FindFirstObjectByType<AudioListener>();
        if (listener == null) return;
        _lowPass = listener.GetComponent<AudioLowPassFilter>();
        if (_lowPass == null)
            _lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
        _lowPass.cutoffFrequency = normalCutoff;
    }

    private void EnsureVignette()
    {
        _vignetteReady = false;
        _vignette = null;

        if (volumeOverride != null)
        {
            if (volumeOverride.profile.TryGet<Vignette>(out _vignette))
            {
                _vignetteBase  = _vignette.intensity.value;
                _vignetteReady = true;
            }
            return;
        }

        // Search all global Volumes for one that actually has a Vignette override.
        // Pick the highest-priority match so scene-specific volumes win over project defaults.
        var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        float bestPriority = float.MinValue;
        foreach (var v in volumes)
        {
            if (!v.isGlobal || v.priority <= bestPriority) continue;
            // Use instanced profile — never sharedProfile (mutates the asset on disk).
            if (!v.profile.TryGet<Vignette>(out var vig)) continue;
            bestPriority   = v.priority;
            _vignette      = vig;
            _vignetteBase  = vig.intensity.value;
            _vignetteReady = true;
        }
    }

    public void PlayDamageFeedback(float duration)
    {
        // Lazy-re-acquire if listener changed (e.g. after a scene load).
        if (_lowPass == null) EnsureLowPass();
        if (!_vignetteReady) EnsureVignette();

        CameraShake.Shake(shakeIntensity, shakeDuration);

        if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
        _feedbackRoutine = StartCoroutine(FeedbackRoutine(duration));
    }

    private IEnumerator FeedbackRoutine(float duration)
    {
        float savedMusic = GameSettings.MusicVolume;
        float savedSFX   = GameSettings.SFXVolume;

        // --- Fade IN (muffle + duck + vignette) ---
        float t = 0f;
        while (t < audioFadeIn)
        {
            float p = t / audioFadeIn;
            ApplyAudio(p, savedMusic, savedSFX);
            ApplyVignette(p);
            t += Time.deltaTime;
            yield return null;
        }
        ApplyAudio(1f, savedMusic, savedSFX);
        ApplyVignette(1f);

        // --- Hold ---
        float holdTime = Mathf.Max(0f, duration - audioFadeIn - audioFadeOut);
        yield return new WaitForSeconds(holdTime);

        // --- Fade OUT ---
        t = 0f;
        while (t < audioFadeOut)
        {
            float p = 1f - (t / audioFadeOut);
            ApplyAudio(p, savedMusic, savedSFX);
            ApplyVignette(p);
            t += Time.deltaTime;
            yield return null;
        }

        RestoreAll(savedMusic, savedSFX);
        _feedbackRoutine = null;
    }

    private void ApplyAudio(float p, float savedMusic, float savedSFX)
    {
        if (_lowPass != null)
            _lowPass.cutoffFrequency = Mathf.Lerp(normalCutoff, muffledCutoff, p);
        SoundManager.SetMusicVolume(Mathf.Lerp(savedMusic, savedMusic * duckMultiplier, p));
        SoundManager.SetSFXVolume(Mathf.Lerp(savedSFX,   savedSFX   * duckMultiplier, p));
    }

    private void ApplyVignette(float p)
    {
        if (!_vignetteReady || _vignette == null) return;
        _vignette.intensity.value = Mathf.Lerp(_vignetteBase, vignettePeak, p);
    }

    private void RestoreAll(float savedMusic, float savedSFX)
    {
        if (_lowPass != null) _lowPass.cutoffFrequency = normalCutoff;
        SoundManager.SetMusicVolume(savedMusic);
        SoundManager.SetSFXVolume(savedSFX);
        if (_vignetteReady && _vignette != null)
            _vignette.intensity.value = _vignetteBase;
    }

    private void OnDisable()
    {
        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }
        RestoreAll(GameSettings.MusicVolume, GameSettings.SFXVolume);
    }

    private void OnDestroy() => OnDisable();
}
