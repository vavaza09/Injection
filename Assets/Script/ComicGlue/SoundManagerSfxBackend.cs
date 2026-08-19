using System;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>
    /// Real-gameplay <see cref="IComicSfxBackend"/> — resolves <see cref="ComicBeatEvent.sfxName"/>
    /// against <c>SoundType</c> and plays through the live <c>SoundManager</c> singleton. Loose in
    /// Assembly-CSharp (not Game.Comic's asmdef) for the same reason <c>ComicPlayer</c> is: both
    /// are Assembly-CSharp types Game.Comic can't reference. Used by <c>ComicPlayer</c> only — the
    /// Comic Editor's preview uses a different backend, since this one depends on
    /// <c>SoundManager.instance</c>, which is only initialized in Play Mode.
    /// </summary>
    public class SoundManagerSfxBackend : IComicSfxBackend
    {
        private readonly Core.Logging.ILogger _logger;

        public SoundManagerSfxBackend(Core.Logging.ILogger logger = null)
        {
            _logger = logger;
        }

        public void PlayOneShot(string sfxName)
        {
            if (!TryResolve(sfxName, out var sfx)) return;
            SoundManager.PlaySound(sfx);
        }

        public object StartRange(string sfxName)
        {
            if (!TryResolve(sfxName, out var sfx)) return null;
            return SoundManager.StartInstance(sfx);
        }

        public void StopRange(object handle)
        {
            if (handle is AudioSource src) SoundManager.StopInstance(src);
        }

        private bool TryResolve(string sfxName, out SoundType sfx)
        {
            if (Enum.TryParse(sfxName, out sfx)) return true;

            if (_logger != null) _logger.LogWarning($"[SoundManagerSfxBackend] Unknown SFX name '{sfxName}'.");
            else Debug.LogWarning($"[SoundManagerSfxBackend] Unknown SFX name '{sfxName}'.");
            return false;
        }
    }
}
