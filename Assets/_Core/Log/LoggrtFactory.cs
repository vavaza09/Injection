namespace Core.Logging
{
    public class LoggerFactory
    {
        private readonly LogConfig _config;

        public LoggerFactory(LogConfig config)
        {
            _config = config;
        }

        public ILogger CreateLogger<T>()
        {
            string className = typeof(T).Name;
            bool isEnabled = _config.IsEnabled(className);
            return new UnityLogger(className, isEnabled);
        }

        
        public ILogger CreateLogger(string context)
        {
            bool isEnabled = _config.IsEnabled(context);
            return new UnityLogger(context, isEnabled);
        }
    }
}