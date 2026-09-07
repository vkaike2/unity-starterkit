using Scripts.Enums;

namespace Scripts.Interfaces
{
    public interface IInteractableEntity
    {
        InteractionPriority Priority { get; }

        bool CanInteract();

        void OnInteraction(InteractionType interactionType, InteractionState interactionState);
    }
}
