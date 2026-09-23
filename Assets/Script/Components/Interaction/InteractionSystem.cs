using System.Collections.Generic;
using UnityEngine;

namespace Game.Components.Interaction
{
    /// <summary>
    /// Generic nearest-interactable dispatcher. Interactables register only while the player is in
    /// range (see <c>DoorSwitchView</c>'s proximity trigger), so the scan here stays small. Root-scoped
    /// singleton (see RootLifetimeScope) because the Player persists across room loads while individual
    /// interactables are scene-local — a scene-scoped registry would go stale on every room transition.
    /// </summary>
    public sealed class InteractionSystem
    {
        private readonly List<IInteractable> _interactables = new List<IInteractable>();

        public void Register(IInteractable interactable)
        {
            if (interactable == null || _interactables.Contains(interactable)) return;
            _interactables.Add(interactable);
        }

        public void Unregister(IInteractable interactable)
        {
            if (interactable == null) return;
            _interactables.Remove(interactable);
        }

        public bool TryInteract(Vector2 fromPosition)
        {
            IInteractable nearest = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int interactableIndex = 0; interactableIndex < _interactables.Count; interactableIndex++)
            {
                var candidate = _interactables[interactableIndex];
                if (candidate?.InteractTransform == null) continue;

                float distanceSqr = ((Vector2)candidate.InteractTransform.position - fromPosition).sqrMagnitude;
                float rangeSqr = candidate.InteractRange * candidate.InteractRange;
                if (distanceSqr > rangeSqr) continue;

                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = candidate;
                }
            }

            if (nearest == null) return false;

            nearest.Interact();
            return true;
        }
    }
}
