using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Enums;
using Scripts.Interfaces;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Enums;
using Vkaike2.StarterKit.Managers;

namespace Scripts.Managers
{
    public partial class MouseManager : MySingleton<MouseManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        private BaseState _currentState;

        private IInteractableEntity _currentInteractable;

        private Camera _camera;

        private readonly List<BaseState> _allStates = new()
        {
            new Idle(),
            new Dragging(),
        };

        private bool _statesAreStarted;

        public bool IsState(State state)
        {
            if (_currentState == null) return false;
            return _currentState.State == state;
        }

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        protected override async Awaitable OnLoad()
        {
            UpdateManager.Instance.Register(
                UpdateType.Update,
                UpdateOrder.Managers,
                MyUpdate);

            InputManager.Instance.OnLeftMouseButton += HandleLeftMouseButton;

            _camera = Camera.main;

            ChangeState(State.Idle);
        }

        protected override void OnDestroy()
        {
            if (UpdateManager.HasInstance)
            {
                UpdateManager.Instance.Unregister(MyUpdate);
            }

            if (InputManager.HasInstance)
            {
                InputManager.Instance.OnLeftMouseButton -= HandleLeftMouseButton;
            }

            base.OnDestroy();
        }

        private void MyUpdate()
        {
            _currentState?.Update();
        }

        private Vector2 GetMouseWorldPosition()
        {
            return _camera.ScreenToWorldPoint(InputManager.Instance.MouseScreenPosition);
        }

        private void HandleLeftMouseButton(InteractionState interactionState)
        {
            _currentState?.OnLeftMouseButton(interactionState);
        }

        private void TryToStartAllStates()
        {
            if (_statesAreStarted) return;

            foreach (var state in _allStates)
            {
                state.Start(this);
            }

            _statesAreStarted = true;
        }

        private void ChangeState(State nextState)
        {
            TryToStartAllStates();

            _currentState?.OnExit();

            _currentState = _allStates.First(e => e.State == nextState);
            _currentState.OnEnter();
        }

        public enum State
        {
            Idle,
            Dragging
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {

        }

        [Serializable]
        private class Components : ValidatableFields
        {
        }
    }
}
