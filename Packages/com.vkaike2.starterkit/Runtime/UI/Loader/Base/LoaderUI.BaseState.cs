using UnityEngine;
using Vkaike2.StarterKit.Base.Extensions;

namespace Vkaike2.StarterKit.UI
{
    public partial class LoaderUI : MonoBehaviour
    {
        private abstract class BaseState
        {
            protected LoaderUI _parent;

            protected LoaderUI.Components _components;
            protected LoaderUI.Configurations _configurations;

            public abstract LoaderUI.State State { get; }

            public virtual void Start(LoaderUI parent)
            {
                _parent = parent;
                _components = parent._components;
                _configurations = parent._configurations;
            }

            public abstract Awaitable OnEnter();
            public abstract Awaitable OnExit();

            protected async Awaitable LoadTransition(bool toOpen)
            {
                _components.Container.SetActive(true);
                _components.Animator.PlayAnimation(toOpen ? _configurations.AnimationOpening : _configurations.AnimationClosing);

                await _parent.WaitWhileTransitioning();

                _parent.ChangeState(toOpen ? State.Open : State.Closed);
            }
        }
    }
}
