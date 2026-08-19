namespace Game.Comic
{
    /// <summary>
    /// Actually plays/stops SFX on behalf of a <see cref="ComicSfxDispatcher"/>. Kept as an
    /// interface — rather than the dispatcher calling <c>SoundManager</c> directly — because
    /// there are two genuinely different playback contexts and neither can share the other's
    /// implementation:
    /// <list type="bullet">
    /// <item>Real gameplay (<c>ComicPlayer</c>) uses <c>SoundManager</c>'s live singleton and its
    /// pooled AudioSources.</item>
    /// <item>The Comic Editor's preview (<c>ComicEditorWindow</c>) has no such singleton to call —
    /// <c>SoundManager.Awake()</c> only initializes it when <c>Application.isPlaying</c>, so it
    /// simply doesn't exist outside Play Mode, independent of any assembly/reference concern — and
    /// instead sources clip data straight from the SoundManager prefab asset via its own
    /// editor-owned AudioSources.</item>
    /// </list>
    /// Both implementations live outside this asmdef (SoundType/SoundManager are Assembly-CSharp
    /// types Game.Comic can't reference), so this interface is the seam between them and the
    /// portable <see cref="ComicSfxDispatcher"/> reconciliation logic.
    /// </summary>
    public interface IComicSfxBackend
    {
        /// <summary>Fire-and-forget one-shot; a no-op if the name doesn't resolve to a known SFX.</summary>
        void PlayOneShot(string sfxName);

        /// <summary>Starts an independently-stoppable instance (looping or not, per how the SFX is
        /// authored) and returns an opaque handle to pass to <see cref="StopRange"/> — or null if
        /// it couldn't start (unknown name, pool exhausted, etc.); a null handle is later treated
        /// as "nothing to stop".</summary>
        object StartRange(string sfxName);

        /// <summary>Stops an instance previously returned by <see cref="StartRange"/>. Must accept
        /// null (from a StartRange that failed) as a silent no-op.</summary>
        void StopRange(object handle);
    }
}
