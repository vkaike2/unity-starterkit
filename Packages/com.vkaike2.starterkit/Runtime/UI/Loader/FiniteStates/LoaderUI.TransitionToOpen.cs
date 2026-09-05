using UnityEngine;
using Vkaike2.StarterKit.Base.Extensions;

namespace Vkaike2.StarterKit.UI
{
    public partial class LoaderUI : MonoBehaviour
    {
        private class TransitionToOpen : BaseState
        {
            public override State State => State.TransitionToOpen;

            public override async Awaitable OnEnter()
            {
                await LoadTransition(toOpen: true);
            }

            public override async Awaitable OnExit()
            {
            }
        }
    }
}
