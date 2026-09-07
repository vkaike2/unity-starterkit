namespace Scripts.Entities.Player
{
    public partial class PlayerEntity
    {
        private class Idle : BaseState
        {
            public override State State => State.Idle;

            public override void OnEnter()
            {
                _components.ArtPosition.position = _initialPosition;
            }

            public override void OnExit()
            {
            }
        }
    }
}
