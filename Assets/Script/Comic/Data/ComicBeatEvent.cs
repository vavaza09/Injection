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

        [Tooltip("If set, switches background music. Must match a MusicType enum name exactly (e.g. \"BOSS\"). Leave empty to leave music unchanged.")]
        public string musicName = "";

        [Tooltip("Screen-shake amplitude in reference-resolution pixels. 0 = no shake.")]
        [Min(0f)] public float shakeAmplitude;

        [Tooltip("Screen-shake duration in seconds.")]
        [Min(0f)] public float shakeDuration;

        [Tooltip("Auto-play only: seconds to dwell on this beat before auto-advancing.")]
        [Min(0f)] public float autoAdvanceAfter = 2f;
    }
}
