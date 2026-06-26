using System.Collections;
using UnityEngine;

/// <summary>
/// Per-sound Inspector knobs: delay offset, volume override, and mute toggle.
/// All boss attack scripts expose one SfxCue per sound so you can tune timing
/// live in the Inspector without touching the SoundManager asset.
/// </summary>
[System.Serializable]
public struct SfxCue
{
    [Tooltip("Seconds to wait after the phase starts before this sound plays. 0 = immediate.")]
    public float delay;

    [Range(0f, 1f)]
    [Tooltip("Per-sound volume. 0 = use the SoundManager default for this clip.")]
    public float volume;

    [Tooltip("Silence this sound without removing it from the Inspector.")]
    public bool mute;
}

/// <summary>
/// Static helper — plays a SfxCue through a given AudioSource.
/// The MonoBehaviour host is needed only when delay > 0 (coroutine owner).
/// </summary>
public static class BossSfx
{
    public static void Play(MonoBehaviour host, SoundType sound, SfxCue cue, AudioSource source)
    {
        if (cue.mute || source == null) return;

        float vol = cue.volume > 0f ? cue.volume : -1f; // -1 = SoundManager clip default

        if (cue.delay <= 0f)
        {
            SoundManager.PlaySoundOn(sound, source, vol);
        }
        else
        {
            host.StartCoroutine(PlayAfter(sound, cue.delay, vol, source));
        }
    }

    private static IEnumerator PlayAfter(SoundType sound, float delay, float vol, AudioSource source)
    {
        yield return new WaitForSeconds(delay);
        SoundManager.PlaySoundOn(sound, source, vol);
    }
}
