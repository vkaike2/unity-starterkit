using UnityEngine;
using Vkaike2.StarterKit.Base.Extensions;

namespace Vkaike2.StarterKit.UI
{
    public partial class LoaderUI : MonoBehaviour
    {
        private class TransitionToClose : BaseState
        {
            public override State State => State.TransitionToClose;

            public override async Awaitable OnEnter()
            {
                await LoadTransition(toOpen: false);
            }

            public override async Awaitable OnExit()
            {
            }
        }
    }
}
