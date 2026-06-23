public interface ISlowMotionController
{
    void StartSlowMotion(float timeScale, float duration);
    void StartSlowMotionSmooth(float timeScale, float duration, float easeInDuration = 0.2f, float easeOutDuration = 0.2f);
    void StopSlowMotion();

    /// <summary>
    /// Hard reset: cancels all slow-motion AND hitstop and restores Time.timeScale to 1x.
    /// Unlike <see cref="StopSlowMotion"/>, this ignores the hitstop guard — use it for
    /// lifecycle events (player death, scene unload) that must always restore normal time.
    /// </summary>
    void ResetImmediate();
}
