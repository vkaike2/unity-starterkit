using System;
using System.Collections.Generic;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Enums;

namespace Vkaike2.StarterKit.Managers
{
    public class UpdateManager : Singleton<UpdateManager>, ILoadableEntity
    {
        private readonly UpdateChannel _updates = new();
        private readonly UpdateChannel _fixedUpdates = new();
        private readonly UpdateChannel _lateUpdates = new();

        public void Register<TOrder>(UpdateType type, TOrder order, Action action) 
            where TOrder : struct, Enum
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            GetChannel(type).Register(Convert.ToInt32(order), action);
        }

        public int Unregister(Action action)
        {
            if (action == null) return 0;

            return _updates.Unregister(action)
                   + _fixedUpdates.Unregister(action)
                   + _lateUpdates.Unregister(action);
        }

        public void UnregisterAll()
        {
            _updates.Clear();
            _fixedUpdates.Clear();
            _lateUpdates.Clear();
        }

        private void Update() => _updates.Run();
        private void FixedUpdate() => _fixedUpdates.Run();
        private void LateUpdate() => _lateUpdates.Run();

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnregisterAll();
        }

        private UpdateChannel GetChannel(UpdateType type) => type switch
        {
            UpdateType.Update => _updates,
            UpdateType.FixedUpdate => _fixedUpdates,
            UpdateType.LateUpdate => _lateUpdates,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type), type, $"Unhandled {nameof(UpdateType)}."),
        };

        private class Subscription
        {
            public readonly int Order;
            public readonly Action Action;

            private readonly UnityEngine.Object _owner;

            private readonly bool _hasOwner;

            public bool ShouldDelete;

            public Subscription(int order, Action action)
            {
                Order = order;
                Action = action;

                _owner = action.Target as UnityEngine.Object;
                _hasOwner = _owner != null;
            }

            public bool IsDead => ShouldDelete || (_hasOwner && _owner == null);
        }

        private class UpdateChannel
        {
            private readonly List<Subscription> _subscriptions = new();

            private readonly List<Subscription> _pending = new();

            private bool _isRunning;
            private bool _hasDeadSubscriptions;

            public void Register(int order, Action action)
            {
                var subscription = new Subscription(order, action);

                if (_isRunning) _pending.Add(subscription);
                else Insert(subscription);
            }

            public int Unregister(Action action)
            {
                var removed = FlagAsDeleted(_subscriptions, action) + FlagAsDeleted(_pending, action);
                if (removed > 0) _hasDeadSubscriptions = true;

                return removed;
            }

            public void Clear()
            {
                _pending.Clear();

                if (!_isRunning)
                {
                    _subscriptions.Clear();
                    return;
                }

                foreach (var subscription in _subscriptions) subscription.ShouldDelete = true;
                _hasDeadSubscriptions = true;
            }

            public void Run()
            {
                _isRunning = true;
                try
                {
                    foreach (var subscription in _subscriptions)
                    {
                        if (subscription.IsDead)
                        {
                            _hasDeadSubscriptions = true;
                            continue;
                        }

                        Invoke(subscription.Action);
                    }
                }
                finally
                {
                    _isRunning = false;
                }

                MergePending();
                TryToRemoveDeadSubscriptions();
            }

            private static int FlagAsDeleted(List<Subscription> subscriptions, Action action)
            {
                var flagged = 0;
                foreach (var subscription in subscriptions)
                {
                    if (subscription.Action != action || subscription.ShouldDelete) continue;

                    subscription.ShouldDelete = true;
                    flagged++;
                }

                return flagged;
            }

            private static void Invoke(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            private void TryToRemoveDeadSubscriptions()
            {
                if (!_hasDeadSubscriptions) return;

                _subscriptions.RemoveAll(static subscription => subscription.IsDead);
                _hasDeadSubscriptions = false;
            }

            private void MergePending()
            {
                if (_pending.Count == 0) return;

                foreach (var subscription in _pending) Insert(subscription);
                _pending.Clear();
            }

            private void Insert(Subscription subscription)
            {
                var index = _subscriptions.Count;
                while (index > 0 && _subscriptions[index - 1].Order > subscription.Order) index--;

                _subscriptions.Insert(index, subscription);
            }
        }
    }
}
