using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Vkaike2.StarterKit.Attributes;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Extensions;

namespace Vkaike2.StarterKit.UI
{
    public partial class LoaderUI : MonoBehaviour
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        private BaseState? _currentState;

        private static List<BaseState> _allStates = new List<BaseState>()
        {
            new Open(),
            new Closed(),
            new TransitionToOpen(),
            new TransitionToClose(),
        };

        private bool _statesAreStarted = false;

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        public bool IsState(State state)
        {
            if (_currentState == null) return false;
            return _currentState.State == state;
        }

        public void InitiateLoader(State initialState)
        {
            _configurations.Initialize();
            ChangeState(initialState);
        }

        public async Awaitable ToggleLoader(bool open)
        {
            await WaitWhileTransitioning();

            if (IsState(open ? State.Open : State.Closed)) return;

            ChangeState(open ? State.TransitionToOpen : State.TransitionToClose);

            await WaitUntilStateIs(open ? State.Open : State.Closed);
        }

        private async Awaitable WaitWhileTransitioning()
        {
            await Awaitable.NextFrameAsync(destroyCancellationToken);

            while (_components.Animator.IsPlayingAnimations(_configurations.GetTransitionAnimations()))
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
        }

        private async Awaitable WaitUntilStateIs(State state)
        {
            while (!IsState(state))
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
        }
        
        [Button(Header = "Debug", SpaceBefore = 8, Order = 10)]
        private void ToggleVisibility()
        {
            _components.Container.SetActive(!_components.Container.activeSelf);
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

            if (_currentState != null)
            {
                _currentState.OnExit();
            }

            _currentState = _allStates.First(e => e.State == nextState);
            _currentState.OnEnter();
        }

        public enum State
        {
            Open,
            Closed,
            TransitionToOpen,
            TransitionToClose
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {
            [field: SerializeField] public List<Sprite> Images { get; set; }

            [Header("Animations")]
            [SerializeField] private string _animationClosing = "Anim_LoaderUI_Closing";
            [SerializeField] private string _animationClosed = "Anim_LoaderUI_Closed";
            [SerializeField] private string _animationOpening = "Anim_LoaderUI_Opening";
            [SerializeField] private string _animationOpened = "Anim_LoaderUI_Opened";

            public int AnimationClosing { get; private set; }
            public int AnimationClosed { get; private set; }
            public int AnimationOpening { get; private set; }
            public int AnimationOpened { get; private set; }


            private List<int> _transitionAnimations = null;

            public void Initialize()
            {
                AnimationClosing = Animator.StringToHash(_animationClosing);
                AnimationClosed = Animator.StringToHash(_animationClosed);
                AnimationOpening = Animator.StringToHash(_animationOpening);
                AnimationOpened = Animator.StringToHash(_animationOpened);
            }

            public List<int> GetTransitionAnimations()
            {
                if (_transitionAnimations != null) return _transitionAnimations;

                _transitionAnimations = new List<int>()
                {
                    AnimationClosing,
                    AnimationOpening,
                };

                return _transitionAnimations;
            }

        }
        [Serializable]
        private class Components : ValidatableFields
        {
            [field: SerializeField] public Image Image { get; private set; }
            [field: SerializeField] public GameObject Container { get; private set; }
            [field: SerializeField] public Animator Animator { get; set; }

            protected override void Validate()
            {
                ValidateNull(Container, nameof(Container));
                ValidateNull(Animator, nameof(Animator));
            }
        }
    }
}
