using UnityEngine;
using VContainer.Unity;
using VContainer;
using Core.Logging;
using Game.Components.Health;
using Game.Components.Movement;
using Game.Characters.Player;

public class GameLifetime : LifetimeScope
{
    [Header("Logging")]
    [SerializeField] private LogConfig logConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        if (logConfig == null)
        {
            logConfig = ScriptableObject.CreateInstance<LogConfig>();
            Debug.LogWarning("[GameLifetime] LogConfig not assigned: using default");
        }

        // Register LoggerFactory as Singleton
        builder.Register<LoggerFactory>(resolver => 
            new LoggerFactory(logConfig),
            Lifetime.Singleton);

        // Register plain C# components
        builder.Register<HealthComponent>(Lifetime.Transient);
        builder.Register<MovementComponent>(Lifetime.Transient);
        builder.Register<DashComponent>(Lifetime.Transient);

        // Register Player Controllers (Plain C# - No MonoBehaviour)
        builder.Register<PlayerInputHandler>(Lifetime.Singleton);
        
        // PlayerAnimationController requires Animator
        builder.Register<PlayerAnimationController>(resolver =>
        {
            var player = FindAnyObjectByType<Player>();
            var animator = player?.GetComponentInChildren<Animator>();
            var loggerFactory = resolver.Resolve<LoggerFactory>();
            return new PlayerAnimationController(animator, loggerFactory);
        }, Lifetime.Singleton);

        // PlayerAudioController requires Transform and AudioClips
        builder.Register<PlayerAudioController>(resolver =>
        {
            var player = FindAnyObjectByType<Player>();
            var loggerFactory = resolver.Resolve<LoggerFactory>();
            
            // Get AudioClips via SerializedField reflection (simplified for demo)
            return new PlayerAudioController(
                player?.transform,
                null, // jumpSound - assign via Inspector or load from Resources
                null, // attackSound
                null, // hurtSound
                loggerFactory
            );
        }, Lifetime.Singleton);

        // Register MonoBehaviour components in hierarchy
        builder.RegisterComponentInHierarchy<character>();
        builder.RegisterComponentInHierarchy<Player>();
    }
}
