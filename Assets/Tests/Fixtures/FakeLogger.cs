using System.Collections.Generic;

namespace Game.Tests.Fixtures
{
    public class FakeLogger : Core.Logging.ILogger
    {
        public List<string> Logs { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public string LastLog => Logs.Count > 0 ? Logs[Logs.Count - 1] : null;
        public string LastWarning => Warnings.Count > 0 ? Warnings[Warnings.Count - 1] : null;
        public string LastError => Errors.Count > 0 ? Errors[Errors.Count - 1] : null;

        public void Log(string message) => Logs.Add(message);
        public void LogWarning(string message) => Warnings.Add(message);
        public void LogError(string message) => Errors.Add(message);

        public void Clear()
        {
            Logs.Clear();
            Warnings.Clear();
            Errors.Clear();
        }
    }
}
