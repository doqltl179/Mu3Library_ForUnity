using System;
using System.Threading;

namespace Mu3Library.Foundation.Event
{
    public sealed partial class SubscribeHandler
    {
        public uint SubscribeOnce(Action<Action> subscribe, Action<Action> unsubscribe, Action callback)
            => SubscribeOnce(subscribe, unsubscribe, callback, null);

        public uint SubscribeOnce(Action<Action> subscribe, Action<Action> unsubscribe, Action callback, Action onDisposed)
        {
            if (subscribe == null || unsubscribe == null || callback == null)
            {
                return 0;
            }

            uint id = 0;
            int callbackInvoked = 0;

            void ActionOnce()
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                {
                    return;
                }

                EventExceptionUtility.InvokeAndAggregate(
                    () => Deregister(id),
                    callback,
                    $"One-shot subscription failed. id: {id}");
            }

            id = Register(
                () => subscribe(ActionOnce),
                () => unsubscribe(ActionOnce),
                onDisposed
            );

            if (id == 0)
            {
                return 0;
            }

            try
            {
                Subscribe(id);
            }
            catch (Exception exception)
            {
                try
                {
                    Deregister(id);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"One-shot subscription registration failed. id: {id}",
                        exception,
                        cleanupException);
                }

                throw;
            }

            return id;
        }

        public uint SubscribeOnce<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe, Action<T> callback)
            => SubscribeOnce(subscribe, unsubscribe, callback, null);

        public uint SubscribeOnce<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe, Action<T> callback, Action onDisposed)
        {
            if (subscribe == null || unsubscribe == null || callback == null)
            {
                return 0;
            }

            uint id = 0;
            int callbackInvoked = 0;

            void ActionOnce(T arg)
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                {
                    return;
                }

                EventExceptionUtility.InvokeAndAggregate(
                    () => Deregister(id),
                    () => callback(arg),
                    $"One-shot subscription failed. id: {id}");
            }

            id = Register(
                () => subscribe(ActionOnce),
                () => unsubscribe(ActionOnce),
                onDisposed
            );

            if (id == 0)
            {
                return 0;
            }

            try
            {
                Subscribe(id);
            }
            catch (Exception exception)
            {
                try
                {
                    Deregister(id);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"One-shot subscription registration failed. id: {id}",
                        exception,
                        cleanupException);
                }

                throw;
            }

            return id;
        }

        public uint SubscribeOnce<T1, T2>(Action<Action<T1, T2>> subscribe, Action<Action<T1, T2>> unsubscribe, Action<T1, T2> callback)
            => SubscribeOnce(subscribe, unsubscribe, callback, null);

        public uint SubscribeOnce<T1, T2>(Action<Action<T1, T2>> subscribe, Action<Action<T1, T2>> unsubscribe, Action<T1, T2> callback, Action onDisposed)
        {
            if (subscribe == null || unsubscribe == null || callback == null)
            {
                return 0;
            }

            uint id = 0;
            int callbackInvoked = 0;

            void ActionOnce(T1 arg1, T2 arg2)
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                {
                    return;
                }

                EventExceptionUtility.InvokeAndAggregate(
                    () => Deregister(id),
                    () => callback(arg1, arg2),
                    $"One-shot subscription failed. id: {id}");
            }

            id = Register(
                () => subscribe(ActionOnce),
                () => unsubscribe(ActionOnce),
                onDisposed
            );

            if (id == 0)
            {
                return 0;
            }

            try
            {
                Subscribe(id);
            }
            catch (Exception exception)
            {
                try
                {
                    Deregister(id);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"One-shot subscription registration failed. id: {id}",
                        exception,
                        cleanupException);
                }

                throw;
            }

            return id;
        }
    }
}
