namespace Game.Pause
{
    /// <summary>
    /// Reference-counted hard pause. Multiple independent systems (pause menu, victory screen,
    /// comic playback, ...) can each hold a pause under their own handle; Time.timeScale only
    /// returns to 1 once every handle has been released, so overlapping pauses can never stomp
    /// on each other.
    /// </summary>
    public interface IPauseService
    {
        bool IsPaused { get; }
        void Push(string handle);
        void Release(string handle);
    }
}
