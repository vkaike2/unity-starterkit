using Scripts.Enums;
using UnityEngine;

namespace Scripts.Interfaces
{
    public interface IInteractableEntity
    {
        InteractionPriority Priority { get; }

        bool CanInteract();

        void OnInteraction(InteractionType interactionType, InteractionState interactionState);

        void OnDrag(Vector2 worldPosition);
    }
}
