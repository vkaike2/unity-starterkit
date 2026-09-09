using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Enums;
using Scripts.Interfaces;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Enums;
using Vkaike2.StarterKit.Managers;
using Vkaike2.StarterKit.ScriptableObjects;

namespace Scripts.Entities.Player
{
    public partial class PlayerEntity : MonoBehaviour, IInteractableEntity
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public InteractionPriority Priority => InteractionPriority.Player;

        private BaseState _currentState;

        private BoardTile? _currentTile;

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

        public async Awaitable Initialize(BoardTile initialTile)
        {
            UpdateManager.Instance.Register(
                UpdateType.FixedUpdate,
                UpdateOrder.Entities,
                MyFixedUpdate);

            _currentTile = initialTile;

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

        public void OnDrag(Vector2 worldPosition)
        {
            _currentState?.OnDrag(worldPosition);
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
            [field: SerializeField] public Transform SpritePosition { get; private set; }
            [field: SerializeField] public Transform ShadowPosition { get; private set; }
            [field: SerializeField] public Transform GroundPosition { get; private set; }

            [field: Header("Sound Effects")]
            [field: SerializeField] public SoAudioTrack PlacementSoundEffect { get; set; }

            protected override void Validate()
            {
                ValidateNull(SpritePosition, nameof(SpritePosition));
                ValidateNull(ShadowPosition, nameof(ShadowPosition));
                ValidateNull(GroundPosition, nameof(GroundPosition));
            }
        }
    }
}
