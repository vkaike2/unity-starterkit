using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vkaike2.StarterKit.Base.Extensions
{
    public static class AnimatorExtension
    {
        public static void PlayAnimation(this Animator animator, int animation, int layer = 0)
        {
            if (!animator.CanPlay(layer)) return;

            animator.Play(animation, layer);
        }

        public static bool IsPlayingAnimation(this Animator animator, int animation, int layer = 0)
        {
            if (!animator.CanBeRead()) return false;

            var animationLayer = animator.GetCurrentAnimatorStateInfo(layer);
            return animationLayer.shortNameHash == animation || animationLayer.fullPathHash == animation;
        }

        public static bool IsPlayingAnimations(this Animator animator, List<int> animations, int layer = 0)
        {
            if (!animator.CanBeRead()) return false;

            var animationLayer = animator.GetCurrentAnimatorStateInfo(layer);
            return animations.Any(e => animationLayer.shortNameHash == e || animationLayer.fullPathHash == e);
        }


        private static bool CanBeRead(this Animator animator)
        {
            if (animator == null) return false;
            if (!animator.gameObject.activeInHierarchy) return false;

            return true;
        }

        private static bool CanPlay(this Animator animator, int layer)
        {
            if (!animator.CanBeRead()) return false;
            if (animator.IsInTransition(layer)) return false;

            return true;
        }
    }
}
