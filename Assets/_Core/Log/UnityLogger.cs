using UnityEngine;

namespace Core.Logging
{
    /// <summary>
    /// Logger implementation using Unity's Debug class.
    /// </summary>
    /// <remarks>
    /// This logger implementation routes all log messages through Unity's Debug class,
    /// prefixing each message with a contextual tag for easier identification in the console.
    /// Logging can be enabled or disabled through the constructor parameter.
    /// </remarks>
    public class UnityLogger : ILogger
    {
        private readonly bool _isEnabled;
        private readonly string _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityLogger"/> class.
        /// </summary>
        /// <param name="context">
        /// A contextual identifier (e.g., class name or system name) that will be 
        /// prepended to all log messages in the format "[context]: message".
        /// </param>
        /// <param name="isEnabled">
        /// Determines whether logging is enabled. When <c>false</c>, all log methods 
        /// will be no-ops. Default is <c>true</c>.
        /// </param>
        public UnityLogger(string context, bool isEnabled = true)
        {
            _context = context;
            _isEnabled = isEnabled;
        }

        /// <summary>
        /// Logs an informational message to the Unity console.
        /// </summary>
        public void Log(string message)
        {
            if (_isEnabled)
                Debug.Log($"[{_context}]: {message}");
        }

        /// <summary>
        /// Logs a warning message to the Unity console.
        /// </summary>
        public void LogWarning(string message)
        {
            if (_isEnabled)
                Debug.LogWarning($"[{_context}]: {message}");
        }

        /// <summary>
        /// Logs an error message to the Unity console.
        /// </summary>
        public void LogError(string message)
        {
            if (_isEnabled)
                Debug.LogError($"[{_context}]: {message}");
        }
    }
}