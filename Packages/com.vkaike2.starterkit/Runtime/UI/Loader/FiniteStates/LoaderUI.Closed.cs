using UnityEngine;
using Vkaike2.StarterKit.Base.Extensions;

namespace Vkaike2.StarterKit.UI
{
    public partial class LoaderUI : MonoBehaviour
    {
        private class Closed : BaseState
        {
            public override State State => State.Closed;

            public override async Awaitable OnEnter()
            {
                _components.Container.SetActive(true);
                _components.Animator.PlayAnimation(_configurations.AnimationClosed);
            }

            public override async Awaitable OnExit()
            {
            }
        }
    }
}
