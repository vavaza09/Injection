namespace Game.Comic
{
    /// <summary>
    /// Dispatches point-in-time music-switch beat events to an injected <see cref="IComicMusicBackend"/>.
    /// Unlike SFX, music has no range/delay — just "when this beat activates, switch to this
    /// track" — and relies on the backend to no-op a repeat request for the track already playing
    /// (matching <c>SoundManager.PlayMusic</c>'s own dedup), so calling <see cref="Reconcile"/>
    /// again at an unchanged beat (e.g. an editor rebuild after an unrelated field edit) is always
    /// safe without needing its own idempotency guard.
    /// <para/>
    /// Currently used by the Comic Editor's preview only — <c>ComicPlayer</c> still dispatches
    /// music inline in its own already-working, already-verified beat-event loop; unifying it onto
    /// this shared type is a straightforward follow-up, not done here to avoid touching working
    /// runtime code while fixing an editor-only gap.
    /// </summary>
    public class ComicMusicDispatcher
    {
        private readonly IComicMusicBackend _backend;

        public ComicMusicDispatcher(IComicMusicBackend backend)
        {
            _backend = backend;
        }

        public void Reconcile(ComicPage page, int beat)
        {
            if (page == null) return;

            for (int i = 0; i < page.beatEvents.Count; i++)
            {
                var e = page.beatEvents[i];
                if (e.beatIndex == beat && !string.IsNullOrEmpty(e.musicName))
                    _backend.PlayMusic(e.musicName);
            }
        }
    }
}
