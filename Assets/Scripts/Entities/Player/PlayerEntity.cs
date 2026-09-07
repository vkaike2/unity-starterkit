using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Enums;
using Scripts.Interfaces;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Enums;
using Vkaike2.StarterKit.Managers;

namespace Scripts.Entities.Player
{
    public partial class PlayerEntity : MonoBehaviour, ILoadableEntity, IInteractableEntity
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public InteractionPriority Priority => InteractionPriority.Player;

        private BaseState _currentState;

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

        public async Awaitable Load()
        {
            UpdateManager.Instance.Register(
                UpdateType.FixedUpdate,
                UpdateOrder.Entities,
                MyFixedUpdate);

            // _currentTile = MapManager.Instance.GetTileAtWorldPosition(
            //     _components.GroundPosition.position,
            //     this);

            // Debug.Log($"[{nameof(PlayerEntity)}] {name} is on tile "
            //           + (_currentTile != null ? _currentTile.Coordinate.ToString() : "none"));

            ChangeState(State.Idle);
        }

        public bool CanInteract()
        {
            return true;
        }

        public void OnInteraction(InteractionType interactionType, InteractionState interactionState)
        {
            if (interactionType != InteractionType.LeftMouseButton) return;

            ChangeState(interactionState == InteractionState.Pressed
                ? State.Dragging
                : State.Idle);
        }

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        private void MyFixedUpdate()
        {
            _currentState?.OnFixedUpdate();
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
            [field: SerializeField] public Transform ArtPosition { get; private set; }
            [field: SerializeField] public Transform GroundPosition { get; private set; }
            [field: SerializeField] public Transform DraggingPosition { get; private set; }

            protected override void Validate()
            {
                ValidateNull(ArtPosition, nameof(ArtPosition));
                ValidateNull(GroundPosition, nameof(GroundPosition));
                ValidateNull(DraggingPosition, nameof(DraggingPosition));
            }
        }
    }
}
