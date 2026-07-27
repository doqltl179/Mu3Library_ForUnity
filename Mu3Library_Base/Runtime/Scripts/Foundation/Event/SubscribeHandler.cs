using System;
using System.Collections.Generic;

namespace Mu3Library.Foundation.Event
{
    /// <summary>
    /// Subscription ID can not be 0. It will be used to indicate invalid subscription.
    /// </summary>
    public sealed partial class SubscribeHandler : IDisposable
    {
        private bool _disposed = false;
        public bool IsDisposed => _disposed;

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
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning("SubscribeHandler is disposed.");
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
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogError("Valid subscription ID not found within retry limit.");
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
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning("SubscribeHandler is disposed.");
                return;
            }

            if (!_subscriptions.TryGetValue(subscriptionId, out var info))
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Subscription ID not found. id: {subscriptionId}");
                return;
            }
            else if (info == null)
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Subscription info is null. id: {subscriptionId}");
                _subscriptions.Remove(subscriptionId);
                return;
            }

            info.Subscribe();
        }

        public void UnSubscribe(uint subscriptionId)
        {
            if (_disposed)
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning("SubscribeHandler is disposed.");
                return;
            }

            if (!_subscriptions.TryGetValue(subscriptionId, out var info))
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Subscription ID not found. id: {subscriptionId}");
                return;
            }
            else if (info == null)
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Subscription info is null. id: {subscriptionId}");
                _subscriptions.Remove(subscriptionId);
                return;
            }

            info.Unsubscribe();
        }
        #endregion
    }
}
