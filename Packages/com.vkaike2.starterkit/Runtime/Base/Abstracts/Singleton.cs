using System;
using UnityEngine;
using Vkaike2.StarterKit.Base.Interfaces;

namespace Vkaike2.StarterKit.Base.Abstracts
{
    public abstract class Singleton<T> : MonoBehaviour, ILoadableEntity where T : Singleton<T>
    {
        public static T Instance { get; private set; }

        public static bool HasInstance => Instance != null;

        public async Awaitable Load()
        {
            RegisterInstance();

            await OnLoad();
        }

#pragma warning disable CS1998
        protected virtual async Awaitable OnLoad()
        {
        }
#pragma warning restore CS1998

        protected void RegisterInstance()
        {
            if (Instance == this) return;

            if (Instance != null)
            {
                throw new InvalidOperationException(
                    $"[{typeof(T).Name}] A second instance was created on '{name}', " +
                    $"but '{Instance.name}' is already registered. " +
                    $"Only one {typeof(T).Name} may exist at a time.");
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
