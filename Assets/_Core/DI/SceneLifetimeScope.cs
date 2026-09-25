using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Spawning;
using Game.Rooms;
using Game.Rooms.Objectives;
using Game.Tutorial;
using Game.Comic;
using Game.Progression;

/// <summary>
/// Child scope placed once in every room scene. Auto-parents to the session
/// <see cref="RootLifetimeScope"/> (see <see cref="FindParent"/>) so it can resolve
/// the persistent Player + session services while owning only room-local objects.
/// </summary>
public class SceneLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        Debug.Log($"[SceneLifetimeScope] Configure running. Parent={(RootLifetimeScope.Instance != null ? "OK" : "NULL")}");

        // Enemy factory captures THIS child resolver so spawned enemies are injected
        // from the scope that can see both scene-local and parent (session) registrations.
        builder.Register<EnemyFactory>(Lifetime.Scoped).As<IEnemyFactory>();

        // Door objective system — scoped per room, mirrors RoomObjectiveManager's per-room lifetime.
        builder.Register<DoorObjectiveEvents>(Lifetime.Scoped).AsSelf().As<IDoorObjectiveEvents>();
        builder.Register<DoorObjectiveSystem>(Lifetime.Scoped);
        builder.Register<DoorCutawaySystem>(Lifetime.Scoped);

        // Scene-local components that need [Inject] but aren't resolved by anything: inject
        // them explicitly after build (RegisterComponentInHierarchy only injects on resolve,
        // so a spawner/trigger that nothing depends on would otherwise never get its deps).
        builder.RegisterBuildCallback(container =>
        {
            Debug.Log("[SceneLifetimeScope] BuildCallback running.");
            InjectAll<RoomObjectiveManager>(container);
            InjectAll<RoomSpawner>(container);
            InjectAll<SavePointTrigger>(container);
            InjectAll<BossPersistence>(container);
            InjectAll<RoomPortal>(container);
            InjectAll<TutorialManager>(container);
            InjectAll<ComicTrigger>(container);
            InjectAll<ComicPlayOnEntry>(container);
            InjectAll<DoorView>(container);
            InjectAll<DoorSwitchView>(container);
            InjectAll<AbilityUnlockTrigger>(container);
            InjectAll<AbilityUnlockOnEntry>(container);

            // Nothing else resolves DoorCutawaySystem (it works purely via its DoorOpened
            // subscription), so force its construction here — otherwise it would never be built
            // and the door-open cutscene would never fire.
            container.Resolve<DoorCutawaySystem>();
            InjectAll<CameraForesightExtension>(container);
        });
    }

    private static void InjectAll<T>(IObjectResolver container) where T : Component
    {
        var items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[SceneLifetimeScope] InjectAll<{typeof(T).Name}>: {items.Length} found.");
        foreach (var item in items)
            container.Inject(item);
    }

    // Robust cross-scene parenting: the room scene is loaded at runtime, so an inspector
    // parentReference cannot survive the scene boundary. Point straight at the live root.
    protected override LifetimeScope FindParent()
    {
        return RootLifetimeScope.Instance;
    }
}
