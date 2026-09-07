using System.Linq;
using Scripts.Enums;
using Scripts.Interfaces;
using UnityEngine;
using Vkaike2.StarterKit.Base.Utils;

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

            public virtual void Update()
            {
            }

            public virtual void OnLeftMouseButton(InteractionState interactionState)
            {
            }

            protected bool TryGetInteractableUnderMouse(out IInteractableEntity interactable)
            {
                interactable = RayCastUtils
                    .GetComponentsAtPosition<IInteractableEntity>(_parent.GetMouseWorldPosition())
                    .Where(entity => entity.CanInteract())
                    .OrderByDescending(entity => entity.Priority)
                    .FirstOrDefault();

                return interactable != null;
            }
        }
    }
}
