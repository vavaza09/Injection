using UnityEngine;

namespace Game.Components.Interaction
{
    public interface IInteractable
    {
        Transform InteractTransform { get; }
        float InteractRange { get; }
        void Interact();
    }
}
