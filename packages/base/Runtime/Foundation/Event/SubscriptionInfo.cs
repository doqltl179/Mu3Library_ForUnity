using System;
using Mu3Library.Foundation.Logging;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

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
                Mu3Logger.Current.LogWarning($"Subscription is disposed. id: {_id}");
                return;
            }
            else if (_subscribed)
            {
                Mu3Logger.Current.LogWarning($"Already subscribed. id: {_id}");
                return;
            }

            _subscribed = true;
            try
            {
                // Set the state before invoking the hook so a synchronously fired
                // one-shot event can deregister this subscription safely.
                _subscribe?.Invoke();
            }
            catch (Exception exception)
            {
                if (!_disposed)
                {
                    _subscribed = false;

                    try
                    {
                        // Best-effort rollback for sources whose subscribe hook
                        // partially completed before throwing.
                        _unsubscribe?.Invoke();
                    }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException(
                            $"Subscription activation failed. id: {_id}",
                            exception,
                            rollbackException);
                    }
                }

                throw;
            }
        }

        public void Unsubscribe()
        {
            if (_disposed)
            {
                Mu3Logger.Current.LogWarning($"Subscription is disposed. id: {_id}");
                return;
            }

            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            try
            {
                _unsubscribe?.Invoke();
            }
            catch
            {
                if (!_disposed)
                {
                    _subscribed = true;
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            List<Exception> exceptions = null;

            try
            {
                Unsubscribe();
            }
            catch (Exception exception)
            {
                exceptions = new List<Exception> { exception };
            }

            // A failed external unsubscribe must not leave this object reusable.
            // The external source may be in an unknown state, but this token is
            // no longer able to retry after disposal.
            _disposed = true;
            _subscribed = false;

            Action onDisposed = _onDisposed;
            _subscribe = null;
            _unsubscribe = null;
            _onDisposed = null;

            try
            {
                onDisposed?.Invoke();
            }
            catch (Exception exception)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(exception);
            }

            EventExceptionUtility.ThrowIfAny(
                exceptions,
                $"Subscription disposal failed. id: {_id}");
        }
    }

    internal static class EventExceptionUtility
    {
        public static void InvokeAndAggregate(Action first, Action second, string message)
        {
            List<Exception> exceptions = null;

            try
            {
                first?.Invoke();
            }
            catch (Exception exception)
            {
                exceptions = new List<Exception> { exception };
            }

            try
            {
                second?.Invoke();
            }
            catch (Exception exception)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(exception);
            }

            ThrowIfAny(exceptions, message);
        }

        public static void ThrowIfAny(List<Exception> exceptions, string message)
        {
            if (exceptions == null || exceptions.Count == 0)
            {
                return;
            }

            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
                return;
            }

            throw new AggregateException(message, exceptions);
        }
    }
}
