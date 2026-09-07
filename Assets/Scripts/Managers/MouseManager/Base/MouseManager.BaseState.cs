namespace Scripts.Managers
{
    public partial class MouseManager
    {
        private abstract class BaseState
        {
            protected MouseManager _parent;

            protected MouseManager.Components _components;
            protected MouseManager.Configurations _configurations;

            public abstract MouseManager.State State { get; }

            public virtual void Start(MouseManager parent)
            {
                _parent = parent;
                _components = parent._components;
                _configurations = parent._configurations;
            }

            public abstract void OnEnter();
            public abstract void OnExit();
            public abstract void Update();
        }
    }
}
