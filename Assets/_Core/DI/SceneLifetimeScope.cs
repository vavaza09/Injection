using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Spawning;
using Game.Rooms;

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

        // Scene-local components that need [Inject] but aren't resolved by anything: inject
        // them explicitly after build (RegisterComponentInHierarchy only injects on resolve,
        // so a spawner/trigger that nothing depends on would otherwise never get its deps).
        builder.RegisterBuildCallback(container =>
        {
            Debug.Log("[SceneLifetimeScope] BuildCallback running.");
            InjectAll<RoomSpawner>(container);
            InjectAll<SavePointTrigger>(container);
            InjectAll<BossPersistence>(container);
            InjectAll<RoomPortal>(container);
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
