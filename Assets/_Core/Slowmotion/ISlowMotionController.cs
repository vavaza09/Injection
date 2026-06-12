public interface ISlowMotionController
{
    void StartSlowMotion(float timeScale, float duration);
    void StartSlowMotionSmooth(float timeScale, float duration, float easeInDuration = 0.2f, float easeOutDuration = 0.2f);
    void StopSlowMotion();
}
