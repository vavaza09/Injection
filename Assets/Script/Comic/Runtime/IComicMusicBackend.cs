namespace Game.Comic
{
    /// <summary>
    /// Actually switches music on behalf of a <see cref="ComicMusicDispatcher"/> — same seam
    /// pattern as <see cref="IComicSfxBackend"/> and for the same reason: real gameplay
    /// (<c>ComicPlayer</c>) and the Comic Editor's preview each need a different implementation
    /// (the Editor can't reach the live <c>SoundManager</c> singleton, which only initializes in
    /// Play Mode), and both are Assembly-CSharp/loose-Editor types Game.Comic can't reference.
    /// </summary>
    public interface IComicMusicBackend
    {
        /// <summary>Requests a switch to the named track. Must be safe to call repeatedly with the
        /// name already playing — e.g. on every beat re-reconcile — without restarting it.</summary>
        void PlayMusic(string musicName);
    }
}
