using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Event
{
    /// <summary>
    /// Subscription ID can not be 0. It will be used to indicate invalid subscription.
    /// </summary>
    public sealed partial class SubscribeHandler : IDisposable
    {
        private bool _disposed = false;
        public bool IsDisposed => _disposed;

        private sealed class SubscriptionInfo : IDisposable
        {
            private uint _id;
            public uint Id => _id;

            private bool _subscribed;
            public bool IsSubscribed => _subscribed;

            private bool _disposed;
            public bool IsDisposed => _disposed;

            private Action _subscribe;
            private Action _unsubscribe;
            private Action _onDisposed;



            public SubscriptionInfo(uint id, Action subscribe, Action unsubscribe)
                : this(id, subscribe, unsubscribe, null)
            {
            }

            public SubscriptionInfo(uint id, Action subscribe, Action unsubscribe, Action onDisposed)
            {
                _subscribed = false;
                _disposed = false;

                _id = id;
                _subscribe = subscribe;
                _unsubscribe = unsubscribe;
                _onDisposed = onDisposed;
            }

            public void Subscribe()
            {
                if (_disposed)
                {
                    Debug.LogWarning($"Subscription is disposed. id: {_id}");
                    return;
                }
                else if (_subscribed)
                {
                    Debug.LogWarning($"Already subscribed. id: {_id}");
                    return;
                }

                _subscribed = true;
                _subscribe?.Invoke();
            }

            public void Unsubscribe()
            {
                if (_disposed)
                {
                    Debug.LogWarning($"Subscription is disposed. id: {_id}");
                    return;
                }

                _subscribed = false;
                _unsubscribe?.Invoke();
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                Unsubscribe();

                _disposed = true;

                Action onDisposed = _onDisposed;
                _subscribe = null;
                _unsubscribe = null;
                _onDisposed = null;
                onDisposed?.Invoke();
            }
        }

        private readonly Dictionary<uint, SubscriptionInfo> _subscriptions = new();
        private uint _latestSubscriptionId = 0;



        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            OnDispose();
        }

        private void OnDispose()
        {
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }

        #region Utility
        public uint Register(Action subscribe, Action unsubscribe)
            => Register(subscribe, unsubscribe, null);

        public uint Register(Action subscribe, Action unsubscribe, Action onDisposed)
        {
            if (_disposed)
            {
                Debug.LogWarning("SubscribeHandler is disposed.");
                return 0;
            }

            SubscriptionInfo containedInfo = null;
            bool foundValidSubscriptionId = false;

            const int retryLimit = 1000;
            for (int i = 0; i < retryLimit; i++)
            {
                _latestSubscriptionId++;

                if (!_subscriptions.TryGetValue(_latestSubscriptionId, out containedInfo) ||
                    containedInfo == null ||
                    containedInfo.IsDisposed)
                {
                    foundValidSubscriptionId = true;
                    break;
                }
            }

            if (!foundValidSubscriptionId)
            {
                Debug.LogError("Valid subscription ID not found within retry limit.");
                return 0;
            }

            var info = new SubscriptionInfo(_latestSubscriptionId, subscribe, unsubscribe, onDisposed);
            _subscriptions[_latestSubscriptionId] = info;

            return _latestSubscriptionId;
        }

        public void Deregister(uint subscriptionId)
        {
            if (_disposed)
            {
                return;
            }

            if (!_subscriptions.TryGetValue(subscriptionId, out var info))
            {
                return;
            }
            else if (info == null)
            {
                _subscriptions.Remove(subscriptionId);
                return;
            }

            info.Dispose();
            _subscriptions.Remove(subscriptionId);
        }

        public void Subscribe(uint subscriptionId)
        {
            if (_disposed)
            {
                Debug.LogWarning("SubscribeHandler is disposed.");
                return;
            }

            if (!_subscriptions.TryGetValue(subscriptionId, out var info))
            {
                Debug.LogWarning($"Subscription ID not found. id: {subscriptionId}");
                return;
            }
            else if (info == null)
            {
                Debug.LogWarning($"Subscription info is null. id: {subscriptionId}");
                _subscriptions.Remove(subscriptionId);
                return;
            }

            info.Subscribe();
        }

        public void UnSubscribe(uint subscriptionId)
        {
            if (_disposed)
            {
                Debug.LogWarning("SubscribeHandler is disposed.");
                return;
            }

            if (!_subscriptions.TryGetValue(subscriptionId, out var info))
            {
                Debug.LogWarning($"Subscription ID not found. id: {subscriptionId}");
                return;
            }
            else if (info == null)
            {
                Debug.LogWarning($"Subscription info is null. id: {subscriptionId}");
                _subscriptions.Remove(subscriptionId);
                return;
            }

            info.Unsubscribe();
        }
        #endregion
    }
}