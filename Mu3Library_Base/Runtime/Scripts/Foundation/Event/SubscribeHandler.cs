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
            List<Exception> exceptions = null;

            foreach (var subscription in new List<SubscriptionInfo>(_subscriptions.Values))
            {
                if (subscription == null)
                {
                    continue;
                }

                try
                {
                    subscription.Dispose();
                }
                catch (Exception exception)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            _subscriptions.Clear();

            EventExceptionUtility.ThrowIfAny(
                exceptions,
                "One or more subscriptions failed to dispose.");
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

            if (!TryGetSubscriptionId(out uint subscriptionId))
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogError("No valid subscription ID is available.");
                return 0;
            }

            var info = new SubscriptionInfo(subscriptionId, subscribe, unsubscribe, onDisposed);
            _subscriptions[subscriptionId] = info;

            return subscriptionId;
        }

        private bool TryGetSubscriptionId(out uint subscriptionId)
        {
            // There are uint.MaxValue usable IDs because zero is reserved as
            // the invalid-subscription sentinel.
            for (ulong attempt = 0; attempt <= uint.MaxValue; attempt++)
            {
                _latestSubscriptionId = unchecked(_latestSubscriptionId + 1);

                if (_latestSubscriptionId == 0)
                {
                    continue;
                }

                if (_subscriptions.TryGetValue(_latestSubscriptionId, out var containedInfo))
                {
                    if (containedInfo != null && !containedInfo.IsDisposed)
                    {
                        continue;
                    }

                    _subscriptions.Remove(_latestSubscriptionId);
                }

                subscriptionId = _latestSubscriptionId;
                return true;
            }

            subscriptionId = 0;
            return false;
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

            try
            {
                info.Dispose();
            }
            finally
            {
                _subscriptions.Remove(subscriptionId);
            }
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
