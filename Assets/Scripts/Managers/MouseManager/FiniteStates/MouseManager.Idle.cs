using Scripts.Enums;

namespace Scripts.Managers
{
    public partial class MouseManager
    {
        private class Idle : BaseState
        {
            public override State State => State.Idle;

            public override void OnEnter()
            {
            }

            public override void OnExit()
            {
            }

            public override void OnLeftMouseButton(InteractionState interactionState)
            {
                if (interactionState != InteractionState.Pressed) return;

                if (!TryGetInteractableUnderMouse(out var interactable)) return;

                _parent._currentInteractable = interactable;

                interactable.OnInteraction(InteractionType.LeftMouseButton, interactionState);

                _parent.ChangeState(State.Dragging);
            }
        }
    }
}
