using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vkaike2.StarterKit.Base.Abstracts;

namespace Scripts.Managers
{
    public class InputManager : MySingleton<InputManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public event Action<ClickState> OnLeftMouseButton;
        public bool IsLeftMouseButtonPressed => 
            _components.LeftMouseButtonAction != null 
            && _components.LeftMouseButtonAction.IsPressed();


        protected override async Awaitable OnLoad()
        {
            ToggleInputSubscriptionToPlayerAction(_components.LeftMouseButtonAction, activating: true);
        }

        protected override void OnDestroy()
        {
            ToggleInputSubscriptionToPlayerAction(_components.LeftMouseButtonAction, activating: false);
            base.OnDestroy();
        }

        private void ToggleInputSubscriptionToPlayerAction(InputAction action, bool activating)
        {
            if (activating)
            {
                action.started += HandleTapStarted;
                action.canceled += HandleTapCanceled;

                action.Enable();
            }
            else
            {
                if (action != null)
                {
                    action.started -= HandleTapStarted;
                    action.canceled -= HandleTapCanceled;
                    action.Disable();
                    action = null;
                }
            }
        }

        private void HandleTapStarted(InputAction.CallbackContext context)
        {
            OnLeftMouseButton?.Invoke(ClickState.Pressed);
        }

        private void HandleTapCanceled(InputAction.CallbackContext context)
        {
            OnLeftMouseButton?.Invoke(ClickState.Released);
        }

        public enum ClickState
        {
            Pressed,
            Released
        }

        [Serializable]
        private class Configurations
        {

        }

        [Serializable]
        private class Components
        {
            [field: SerializeField] private InputActionReference _leftMouseButtonAction;

            public InputAction LeftMouseButtonAction => _leftMouseButtonAction.action;
        }
    }
}
