using UnityEngine;
using VContainer.Unity;
using VContainer;
using Core.Logging;

public class GameLifetime : LifetimeScope
{
    [Header("Logging")]
    [SerializeField] private LogConfig logConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        if (logConfig == null)
        {
            logConfig = ScriptableObject.CreateInstance<LogConfig>();
            Debug.LogWarning("[GameLife] doesn't config: use default");
        }

        builder.Register<LoggerFactory>(resolver => 
            new LoggerFactory(logConfig),
            Lifetime.Singleton);
    }
}
