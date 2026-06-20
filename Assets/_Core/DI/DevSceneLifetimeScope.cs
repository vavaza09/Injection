using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Logging;
using Game.Components.Health;
using Game.Components.Movement;
using Game.Components.Combat;
using Game.Components.Skills;
using Game.Characters.Player;

/// <summary>
/// Self-contained scope for dev/practice scenes (e.g. practice-vava).
/// No Bootstrap dependency, no rooms, no save — registers only what's needed
/// to test player movement, combat, and enemy interactions.
/// Add this component to a GO in any standalone test scene.
/// </summary>
public class DevSceneLifetimeScope : LifetimeScope
{
    [SerializeField] private LogConfig logConfig;
    [SerializeField] private int maxEnergy   = 3;
    [SerializeField] private int startEnergy = 0;

    protected override void Configure(IContainerBuilder builder)
    {
        if (logConfig == null)
        {
            logConfig = ScriptableObject.CreateInstance<LogConfig>();
            Debug.LogWarning("[DevSceneLifetimeScope] LogConfig not assigned: using default");
        }

        builder.Register<LoggerFactory>(_ => new LoggerFactory(logConfig), Lifetime.Singleton);
        builder.Register<ISlowMotionController>(_ => SlowMotion.Instance, Lifetime.Singleton);

        builder.Register<HealthComponent>(Lifetime.Transient);
        builder.Register<MovementComponent>(Lifetime.Transient);
        builder.Register<AttackComponent>(_ => new AttackComponent(0.15f), Lifetime.Transient);

        builder.Register<PlayerInputHandler>(Lifetime.Singleton);
        builder.Register<PlayerAnimationController>(resolver =>
        {
            var p = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
            var anim = p != null ? p.GetComponentInChildren<Animator>() : null;
            return new PlayerAnimationController(anim, resolver.Resolve<LoggerFactory>());
        }, Lifetime.Singleton);
        builder.Register<PlayerAudioController>(Lifetime.Singleton);

        builder.Register<PlayerSkillEvents>(Lifetime.Singleton).As<IPlayerSkillEvents>();
        builder.Register<EnergyPool>(_ => new EnergyPool(maxEnergy, startEnergy), Lifetime.Singleton)
            .As<IEnergyPool>()
            .As<IEnergyStore>();

        // Player GO is a sibling root (not under this scope GO).
        // RegisterInstance does NOT auto-inject — use BuildCallback with container.Inject() instead
        // (same pattern as SceneLifetimeScope for RoomSpawner / SavePointTrigger).
        var player = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
        if (player != null)
        {
            // Register once with both type keys so other services can resolve either
            builder.RegisterInstance(player).As<character>().As<Player>();

            var sc = player.GetComponent<PlayerSkillController>();
            if (sc != null) builder.RegisterInstance(sc);

            var ec = player.GetComponent<PlayerEnergyCollector>();
            if (ec != null) builder.RegisterInstance(ec);
        }
        else
        {
            Debug.LogError("[DevSceneLifetimeScope] No Player found in scene.");
        }

        var empBlast = FindAnyObjectByType<EmpBlastReceiver>(FindObjectsInactive.Include);
        if (empBlast != null)
            builder.RegisterInstance(empBlast);

        // Inject into every component on the Player GO that has [Inject] methods,
        // plus any scene singletons that need DI. container.Inject() only targets the
        // specific component type passed — sibling components must be injected one by one.
        builder.RegisterBuildCallback(container =>
        {
            var p = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
            if (p != null)
            {
                container.Inject(p);

                var mc = p.GetComponent<MovementComponent>();
                if (mc != null) container.Inject(mc);

                var sc = p.GetComponent<PlayerSkillController>();
                if (sc != null) container.Inject(sc);

                var ec = p.GetComponent<PlayerEnergyCollector>();
                if (ec != null) container.Inject(ec);
            }

            var eb = FindAnyObjectByType<EmpBlastReceiver>(FindObjectsInactive.Include);
            if (eb != null) container.Inject(eb);
        });
    }
}
