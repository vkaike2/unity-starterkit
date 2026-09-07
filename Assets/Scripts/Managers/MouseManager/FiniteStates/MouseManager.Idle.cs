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

            public override void Update()
            {
            }
        }
    }
}
