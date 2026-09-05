using UnityEngine;
using Vkaike2.StarterKit.Base.Extensions;

namespace Vkaike2.StarterKit.UI
{
    public partial class LoaderUI : MonoBehaviour
    {
        private class Open : BaseState
        {
            public override State State => State.Open;

            public override async Awaitable OnEnter()
            {
                _components.Container.SetActive(false);

                _components.Animator.PlayAnimation(_configurations.AnimationOpened);
            }

            public override async Awaitable OnExit()
            {
            }
        }
    }
}
