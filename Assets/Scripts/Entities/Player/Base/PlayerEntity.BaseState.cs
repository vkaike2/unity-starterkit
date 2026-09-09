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
            protected Vector3 _initialSpriteLocalPosition;
            protected Vector3 _initialShadowLocalPosition;
            protected BoardTile CurrentTile
            {
                get { return _parent._currentTile; }
                set { _parent._currentTile = value; }
            }

            public abstract State State { get; }

            public virtual void Start(PlayerEntity parent)
            {
                _parent = parent;
                _components = parent._components;
                _configurations = parent._configurations;

                _initialSpriteLocalPosition = _components.SpritePosition.localPosition;
                _initialShadowLocalPosition = _components.ShadowPosition.localPosition;
            }

            public abstract void OnEnter();
            public abstract void OnExit();

            public virtual void OnFixedUpdate()
            {
            }

            public virtual void OnDrag(Vector2 worldPosition)
            {
            }


            protected void SnapToCurrentTile(bool snappingOnlyShadow)
            {
                if (CurrentTile == null) return;

                if (snappingOnlyShadow)
                {
                    _components.ShadowPosition.position = CurrentTile.CenterPosition.position;
                }
                else
                {
                    _parent.transform.position = CurrentTile.CenterPosition.position;
                    _components.ShadowPosition.localPosition = _initialShadowLocalPosition;
                }
            }
        }
    }
}
