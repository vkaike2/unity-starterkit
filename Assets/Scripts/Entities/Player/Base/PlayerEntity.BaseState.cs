using UnityEngine;

namespace Scripts.Entities.Player
{
    public partial class PlayerEntity
    {
        private abstract class BaseState
        {
            protected PlayerEntity _parent;

            protected PlayerEntity.Components _components;
            protected PlayerEntity.Configurations _configurations;

            protected Vector2 _initialPosition;

            public abstract PlayerEntity.State State { get; }

            public virtual void Start(PlayerEntity parent)
            {
                _parent = parent;
                _components = parent._components;
                _configurations = parent._configurations;

                _initialPosition = _components.ArtPosition.position;
            }

            public abstract void OnEnter();
            public abstract void OnExit();

            public virtual void OnFixedUpdate()
            {
            }
        }
    }
}
