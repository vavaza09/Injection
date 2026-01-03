namespace Core.Logging
{
    /// <summary>
    /// Interface for logging messages system.
    /// </summary>
    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}
