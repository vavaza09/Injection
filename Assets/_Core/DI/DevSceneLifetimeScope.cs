using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Logging;
using Game.Components.Health;
using Game.Components.Movement;
using Game.Components.Combat;
using Game.Components.Skills;
using Game.Components.Glide;
using Game.Characters.Player;
using Game.Tutorial;
using Game.Spawning;
using Game.UI;
using Game.Components.Interaction;
using Game.Persistence;
using Game.Progression;
using Game.Rooms.Objectives;

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

    [Header("Glide")]
    [SerializeField] private GlideConfig glideConfig;
    [Tooltip("Abilities (see AbilityIds) unlocked in-memory at scene start via the same " +
             "SaveService this scope already gives every door/objective — no special-case " +
             "bypass, no save file written (SaveService here is InMemorySaveStorage-backed).")]
    [SerializeField] private string[] startUnlockedAbilities;

    protected override void Configure(IContainerBuilder builder)
    {
        if (logConfig == null)
        {
            logConfig = ScriptableObject.CreateInstance<LogConfig>();
            Debug.LogWarning("[DevSceneLifetimeScope] LogConfig not assigned: using default");
        }

        builder.Register<LoggerFactory>(_ => new LoggerFactory(logConfig), Lifetime.Singleton);
        builder.Register<ISlowMotionController>(_ => SlowMotion.Instance, Lifetime.Singleton);

        // Required by Player.Construct() — every scope that resolves Player must provide one.
        builder.Register<InteractionSystem>(Lifetime.Singleton);

        // Door objective system needs a SaveService, but this scope's own doc comment says
        // "no Bootstrap, no rooms, no save" — so back it with an in-memory-only store instead of a
        // real file. Every Play session starts with every door/objective closed, nothing written to disk.
        builder.Register<ISaveStorage>(_ => new InMemorySaveStorage(), Lifetime.Singleton);
        builder.Register<SaveService>(resolver => new SaveService(
            resolver.Resolve<ISaveStorage>(),
            resolver.Resolve<LoggerFactory>().CreateLogger("SaveService")),
            Lifetime.Singleton);
        builder.Register<DoorObjectiveEvents>(Lifetime.Scoped).AsSelf().As<IDoorObjectiveEvents>();
        builder.Register<DoorObjectiveSystem>(Lifetime.Scoped);
        builder.Register<DoorCutawaySystem>(Lifetime.Scoped);

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

        builder.Register<EnemyFactory>(Lifetime.Singleton).As<IEnemyFactory>();

        builder.Register<PlayerSkillEvents>(Lifetime.Singleton).As<IPlayerSkillEvents>();
        builder.Register<EnergyPool>(_ => new EnergyPool(maxEnergy, startEnergy), Lifetime.Singleton)
            .As<IEnergyPool>()
            .As<IEnergyStore>();

        // Glide — same shared-singleton requirement as RootLifetimeScope (see its comment):
        // PlayerGlideController and CompanionFollowerView must observe the SAME GlideModel.
        if (glideConfig == null)
        {
            glideConfig = ScriptableObject.CreateInstance<GlideConfig>();
            Debug.LogWarning("[DevSceneLifetimeScope] GlideConfig not assigned: using default");
        }
        builder.RegisterInstance(glideConfig);
        builder.Register<GlideModel>(Lifetime.Singleton);
        builder.Register<GlideSystem>(Lifetime.Singleton);

        // Player GO is a sibling root (not under this scope GO).
        // RegisterInstance does NOT auto-inject — use BuildCallback with container.Inject() instead
        // (same pattern as SceneLifetimeScope for RoomSpawner / SavePointTrigger).
        var player = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
        if (player != null)
        {
            // Register once with both type keys so other services can resolve either
            builder.RegisterInstance(player).As<character>().As<Player>();

            var sc = player.GetComponent<PlayerSkillController>();
            if (sc != null) builder.RegisterInstance(sc)
                .As<Game.Components.Skills.ISkillReadinessProvider>();

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

        var playerHUD = FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include);
        if (playerHUD != null)
            builder.RegisterInstance(playerHUD);

        var energyHUD = FindAnyObjectByType<Game.UI.Skills.EnergyHUD>(FindObjectsInactive.Include);
        if (energyHUD != null)
            builder.RegisterInstance(energyHUD);

        // Inject into every component on the Player GO that has [Inject] methods,
        // plus any scene singletons that need DI. container.Inject() only targets the
        // specific component type passed — sibling components must be injected one by one.
        builder.RegisterBuildCallback(container =>
        {
            var p = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
            if (p != null)
            {
                // Inject component-level deps first so they are ready regardless of Player injection outcome.
                var mc = p.GetComponent<MovementComponent>();
                if (mc != null) container.Inject(mc);

                var sc = p.GetComponent<PlayerSkillController>();
                if (sc != null) container.Inject(sc);

                var ec = p.GetComponent<PlayerEnergyCollector>();
                if (ec != null) container.Inject(ec);

                var glideController = p.GetComponent<PlayerGlideController>();
                if (glideController != null) container.Inject(glideController);

                var cheats = p.GetComponent<PlayerDebugCheats>();
                if (cheats != null) container.Inject(cheats);

                container.Inject(p);
            }

            // Companion is a scene sibling (not under this scope GO, and not a child of the
            // Player transform — see CompanionFollowerView's doc comment for why).
            var companion = FindAnyObjectByType<CompanionFollowerView>(FindObjectsInactive.Include);
            if (companion != null) container.Inject(companion);

            foreach (var unlockTrigger in FindObjectsByType<AbilityUnlockTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                container.Inject(unlockTrigger);

            if (startUnlockedAbilities != null && startUnlockedAbilities.Length > 0)
            {
                var saveService = container.Resolve<SaveService>();
                foreach (var abilityId in startUnlockedAbilities)
                    saveService.MarkAbilityUnlocked(abilityId);
            }

            var eb = FindAnyObjectByType<EmpBlastReceiver>(FindObjectsInactive.Include);
            if (eb != null) container.Inject(eb);

            // Tutorial orchestrator is a scene sibling (not under this scope GO) — inject it like
            // the Player components above so its [Inject] Construct() receives Player/input/skills.
            var tutorial = FindAnyObjectByType<TutorialManager>(FindObjectsInactive.Include);
            if (tutorial != null) container.Inject(tutorial);

            var roomSpawners = FindObjectsByType<RoomSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rs in roomSpawners) container.Inject(rs);

            if (p != null)
            {
                // Resolve via FindAnyObjectByType, not the .instance singleton — this callback runs
                // during THIS scope's Awake, and Unity does not guarantee this GO's Awake() runs after
                // Main Camera's, so CameraManager.instance can still be null here even though the
                // object already exists in the scene.
                var cameraController = FindAnyObjectByType<CameraController>(FindObjectsInactive.Include);
                cameraController?.SetTarget(p.transform);

                var cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
                cameraManager?.SetFollowTarget(p.transform);

                // Real shipping levels don't use CameraManager/CameraController at all — RoomManager
                // sets Follow directly on the room's CinemachineCamera at runtime (RoomManager.cs).
                // Replicate that here when this dev scene is built from a real level (no legacy
                // manager present), so the follow vcam still gets a target with no RoomManager around.
                if (cameraController == null && cameraManager == null)
                {
                    var vcams = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Unity.Cinemachine.CinemachineCamera followVcam = null;
                    foreach (var vcam in vcams)
                    {
                        // Prefer the vcam with a CinemachinePositionComposer — that's the follow
                        // camera; a fixed cutaway camera (e.g. a door's cutscene vcam) has none.
                        if (vcam.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>() != null)
                        {
                            followVcam = vcam;
                            break;
                        }
                    }
                    if (followVcam == null && vcams.Length > 0) followVcam = vcams[0];

                    if (followVcam != null)
                    {
                        followVcam.Follow = p.transform;
                        followVcam.LookAt = p.transform;
                    }
                }
            }

            var hud = FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include);
            if (hud != null) container.Inject(hud);

            var energyHUD = FindAnyObjectByType<Game.UI.Skills.EnergyHUD>(FindObjectsInactive.Include);
            if (energyHUD != null) container.Inject(energyHUD);

            foreach (var doorView in FindObjectsByType<DoorView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                container.Inject(doorView);
            foreach (var switchView in FindObjectsByType<DoorSwitchView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                container.Inject(switchView);

            // Nothing else resolves DoorCutawaySystem — force its construction so it subscribes.
            container.Resolve<DoorCutawaySystem>();
        });
    }
}
