namespace Scripts.Entities.Player
{
    public partial class PlayerEntity
    {
        private class Dragging : BaseState
        {
            public override State State => State.Dragging;

            public override void OnEnter()
            {
                _components.ArtPosition.position = _components.DraggingPosition.position;
            }

            public override void OnExit()
            {
            }
        }
    }
}
