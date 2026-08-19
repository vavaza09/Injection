using System;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>
    /// Non-visual side effects fired when a beat activates: SFX, a music change, a screen
    /// shake, and (Auto mode only) how long to dwell before auto-advancing.
    /// <para/>
    /// Sfx/music are matched by enum NAME (not a compile-time enum reference): SoundType and
    /// MusicType live in Assembly-CSharp (the default assembly, compiled after every custom
    /// assembly definition), so a normal asmdef — including this one — cannot reference them by
    /// type. Game.Comic stays a portable, engine-agnostic-ish data layer; ComicPlayer (which
    /// does live in Assembly-CSharp) resolves these names via Enum.TryParse at dispatch time and
    /// logs a warning instead of throwing if a name doesn't match.
    /// </summary>
    [Serializable]
    public class ComicBeatEvent
    {
        public int beatIndex;

        [Tooltip("Optional SFX to play when this beat activates. Must match a SoundType enum name exactly (e.g. \"UI_CLICK\"). Leave empty for none.")]
        public string sfxName = "";

        [Tooltip("Seconds to wait after this beat activates before the SFX starts. Cancelled " +
                 "(never fires) if the beat changes before the delay elapses. 0 = play immediately.")]
        [Min(0f)] public float sfxDelay = 0f;

        [Tooltip("Optional: if set (>= 0), the SFX plays continuously from this beat until " +
                 "beatIndex reaches this value (exclusive), then stops — even mid-clip. Leave at " +
                 "-1 for the default: the clip plays once and always runs to completion regardless " +
                 "of how fast beats advance, exactly like a plain one-shot.")]
        public int endBeatIndex = -1;

        [Tooltip("If set, switches background music. Must match a MusicType enum name exactly (e.g. \"BOSS\"). Leave empty to leave music unchanged.")]
        public string musicName = "";

        [Tooltip("Screen-shake amplitude in reference-resolution pixels. 0 = no shake.")]
        [Min(0f)] public float shakeAmplitude;

        [Tooltip("Screen-shake duration in seconds.")]
        [Min(0f)] public float shakeDuration;

        [Tooltip("Auto-play only: seconds to dwell on this beat before auto-advancing.")]
        [Min(0f)] public float autoAdvanceAfter = 2f;

        /// <summary>True if this event's SFX uses beat-range playback instead of a plain one-shot.</summary>
        public bool IsRangeMode => endBeatIndex >= 0;

        /// <summary>Exclusive end of the SFX's active beat window: [beatIndex, EffectiveEndBeat).
        /// Only meaningful when <see cref="IsRangeMode"/> is true.</summary>
        public int EffectiveEndBeat => Mathf.Max(endBeatIndex, beatIndex + 1);

        /// <summary>Whether this event's SFX should be sounding at the given beat: exact match
        /// for a plain one-shot, range membership for range mode. Used by <see cref="ComicSfxDispatcher"/>
        /// to decide start/stop/cancel as a pure function of the current beat — regardless of
        /// whether that beat was reached by stepping one at a time or jumping (e.g. editor scrub).</summary>
        public bool IsActiveAtBeat(int beat) => IsRangeMode ? (beat >= beatIndex && beat < EffectiveEndBeat) : beat == beatIndex;
    }
}
