using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Spawning
{
    /// <summary>
    /// Instantiates enemy prefabs THROUGH the owning (per-scene child) resolver so the
    /// spawned graph is dependency-injected — this is what makes a runtime enemy's
    /// <c>[Inject] InitializeComponent(HealthComponent)</c> actually fire (hand-placed
    /// enemies relied on scene-build injection, which runtime spawns never received).
    /// </summary>
    public sealed class EnemyFactory : IEnemyFactory
    {
        private readonly IObjectResolver _resolver;

        public EnemyFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public Enemy Create(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            var instance = _resolver.Instantiate(prefab, position, rotation);
            return instance.GetComponent<Enemy>();
        }
    }
}
