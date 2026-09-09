using Scripts.Enums;

namespace Scripts.Managers
{
    public partial class MouseManager
    {
        private class Dragging : BaseState
        {
            public override State State => State.Dragging;

            public override void OnEnter()
            {
            }

            public override void OnExit()
            {
                _parent._currentInteractable = null;
            }

            public override void Update()
            {
                _parent._currentInteractable?.OnDrag(_parent.GetMouseWorldPosition());
            }

            public override void OnLeftMouseButton(InteractionState interactionState)
            {
                if (interactionState != InteractionState.Released) return;

                _parent._currentInteractable?
                    .OnInteraction(InteractionType.LeftMouseButton, interactionState);

                _parent.ChangeState(State.Idle);
            }
        }
    }
}
