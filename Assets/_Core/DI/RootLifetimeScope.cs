using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Logging;
using Game.Components.Health;
using Game.Components.Movement;
using Game.Components.Combat;
using Game.Components.Skills;
using Game.Characters.Player;
using Game.Persistence;
using Game.Rooms;

/// <summary>
/// Session root. Lives in the Bootstrap scene, survives every room swap
/// (DontDestroyOnLoad), and hosts the persistent Player + all cross-scene services.
/// Per-room <see cref="SceneLifetimeScope"/> children parent to this via the static
/// <see cref="Instance"/>. Replaces the old per-scene <c>GameLifetime</c>.
/// </summary>
public class RootLifetimeScope : LifetimeScope
{
    public static RootLifetimeScope Instance { get; private set; }

    [Header("Logging")]
    [SerializeField] private LogConfig logConfig;

    [Header("Energy")]
    [SerializeField] private int maxEnergy   = 3;
    [SerializeField] private int startEnergy = 0;

    [Header("Rooms")]
    [SerializeField] private RoomCatalog roomCatalog;

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A root already exists (e.g. if Bootstrap is ever re-loaded) — never build twice.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    protected override void Configure(IContainerBuilder builder)
    {
        if (logConfig == null)
        {
            logConfig = ScriptableObject.CreateInstance<LogConfig>();
            Debug.LogWarning("[RootLifetimeScope] LogConfig not assigned: using default");
        }

        builder.Register<LoggerFactory>(_ => new LoggerFactory(logConfig), Lifetime.Singleton);
        builder.Register<ISlowMotionController>(_ => SlowMotion.Instance, Lifetime.Singleton);

        // Plain C# gameplay components
        builder.Register<HealthComponent>(Lifetime.Transient);
        builder.Register<MovementComponent>(Lifetime.Transient);
        builder.Register<AttackComponent>(_ => new AttackComponent(0.15f), Lifetime.Transient);

        // Player controllers (plain C#)
        builder.Register<PlayerInputHandler>(Lifetime.Singleton);
        builder.Register<PlayerAnimationController>(resolver =>
        {
            var player = FindAnyObjectByType<Player>();
            var animator = player?.GetComponentInChildren<Animator>();
            var loggerFactory = resolver.Resolve<LoggerFactory>();
            return new PlayerAnimationController(animator, loggerFactory);
        }, Lifetime.Singleton);
        builder.Register<PlayerAudioController>(Lifetime.Singleton);

        builder.Register<PlayerSkillEvents>(Lifetime.Singleton).As<IPlayerSkillEvents>();
        builder.Register<EnergyPool>(_ => new EnergyPool(maxEnergy, startEnergy), Lifetime.Singleton)
            .As<IEnergyPool>()
            .As<IEnergyStore>();

        // Persistent Player + on-object controllers (DontDestroyOnLoad alongside this root)
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

        // Persistence (session singletons)
        builder.Register<ISaveStorage>(_ => new JsonFileSaveStorage("save.json"), Lifetime.Singleton);
        builder.Register<SaveService>(resolver => new SaveService(
            resolver.Resolve<ISaveStorage>(),
            resolver.Resolve<LoggerFactory>().CreateLogger("SaveService")),
            Lifetime.Singleton);

        // Rooms (session)
        if (roomCatalog != null)
            builder.RegisterInstance(roomCatalog);
        else
            Debug.LogWarning("[RootLifetimeScope] RoomCatalog not assigned — room loading will fail.");
        builder.RegisterComponentInHierarchy<RoomManager>().As<IRoomLoader>();

        builder.RegisterBuildCallback(container =>
        {
            var p = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
            if (p != null)
            {
                var mc = p.GetComponent<MovementComponent>();
                if (mc != null) container.Inject(mc);
            }
        });

        // Death-respawn coordinator (reworked for the persistent player).
        builder.RegisterComponentInHierarchy<RespawnCoordinator>();

        // Boot/continue + autosave-on-entry (session-scoped, live on the root GO).
        builder.RegisterComponentInHierarchy<SaveBootstrapper>();
        builder.RegisterComponentInHierarchy<RoomAutosaveOnEntry>();
    }
}
