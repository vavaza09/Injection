using UnityEngine;
using VContainer.Unity;
using VContainer;
using Core.Logging;
using Game.Components.Health;
using Game.Components.Movement;
using Game.Components.Combat;
using Game.Components.Skills;
using Game.Characters.Player;
using Game.Persistence;

public class GameLifetime : LifetimeScope
{
    [Header("Logging")]
    [SerializeField] private LogConfig logConfig;

    [Header("Energy")]
    [SerializeField] private int maxEnergy   = 3;
    [SerializeField] private int startEnergy = 0;

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

        // Register SlowMotion as singleton via its interface
        builder.Register<ISlowMotionController>(_ => SlowMotion.Instance, Lifetime.Singleton);

        // Register plain C# components
        builder.Register<HealthComponent>(Lifetime.Transient);
        builder.Register<MovementComponent>(Lifetime.Transient);
        builder.Register<AttackComponent>(resolver => new AttackComponent(0.15f), Lifetime.Transient);

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

        // PlayerAudioController uses SoundManager (no AudioClip dependencies)
        builder.Register<PlayerAudioController>(Lifetime.Singleton);

        // Skill system
        builder.Register<PlayerSkillEvents>(Lifetime.Singleton).As<IPlayerSkillEvents>();
        builder.Register<EnergyPool>(
            _ => new EnergyPool(maxEnergy, startEnergy),
            Lifetime.Singleton)
            .As<IEnergyPool>()
            .As<IEnergyStore>();

        // Register MonoBehaviour components in hierarchy
        builder.RegisterComponentInHierarchy<character>();
        builder.RegisterComponentInHierarchy<Player>();
        builder.RegisterComponentInHierarchy<PlayerSkillController>();
        builder.RegisterComponentInHierarchy<PlayerEnergyCollector>();
        var energyHUD = FindAnyObjectByType<Game.UI.Skills.EnergyHUD>(FindObjectsInactive.Include);
        if (energyHUD != null)
            builder.RegisterComponentInHierarchy<Game.UI.Skills.EnergyHUD>();

        var empBlastReceiver = FindAnyObjectByType<EmpBlastReceiver>(FindObjectsInactive.Include);
        if (empBlastReceiver != null)
            builder.RegisterComponentInHierarchy<EmpBlastReceiver>();
        builder.RegisterComponentInHierarchy<EmpBlastReceiver>();

        // Persistence system
        builder.Register<ISaveStorage>(_ => new JsonFileSaveStorage("save.json"), Lifetime.Singleton);
        builder.Register<SaveService>(resolver => new SaveService(
            resolver.Resolve<ISaveStorage>(),
            resolver.Resolve<LoggerFactory>().CreateLogger("SaveService")),
            Lifetime.Singleton);
        builder.RegisterComponentInHierarchy<SaveApplier>();
        builder.RegisterComponentInHierarchy<RespawnCoordinator>();

        // Inject all SavePointTrigger and BossPersistence instances in the scene
        builder.RegisterBuildCallback(container =>
        {
            var savePoints = FindObjectsByType<SavePointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sp in savePoints)
                container.Inject(sp);

            var bossPersistences = FindObjectsByType<BossPersistence>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var bp in bossPersistences)
                container.Inject(bp);
        });
    }
}
