using System;

namespace Mu3Library.Foundation.Event
{
    public sealed class SubscriptionInfo : ISubscriptionInfo
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
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Subscription is disposed. id: {_id}");
                return;
            }
            else if (_subscribed)
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Already subscribed. id: {_id}");
                return;
            }

            _subscribed = true;
            _subscribe?.Invoke();
        }

        public void Unsubscribe()
        {
            if (_disposed)
            {
                // Logging is intentionally disabled in the Foundation layer for now.
                // Mu3Logger.Current.LogWarning($"Subscription is disposed. id: {_id}");
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
}