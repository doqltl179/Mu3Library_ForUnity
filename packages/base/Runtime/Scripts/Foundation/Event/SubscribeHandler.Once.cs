using System;
using System.Threading;

namespace Mu3Library.Foundation.Event
{
    public sealed partial class SubscribeHandler
    {
        public ISubscriptionInfo SubscribeOnce(Action<Action> subscribe, Action<Action> unsubscribe, Action callback)
            => SubscribeOnce(subscribe, unsubscribe, callback, null);

        public ISubscriptionInfo SubscribeOnce(Action<Action> subscribe, Action<Action> unsubscribe, Action callback, Action onDisposed)
        {
            if (subscribe == null || unsubscribe == null || callback == null)
            {
                return null;
            }

            ISubscriptionInfo subscription = null;
            int callbackInvoked = 0;

            void ActionOnce()
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                {
                    return;
                }

                EventExceptionUtility.InvokeAndAggregate(
                    () => Deregister(subscription),
                    callback,
                    $"One-shot subscription failed. id: {subscription?.Id ?? 0}");
            }

            subscription = Register(
                () => subscribe(ActionOnce),
                () => unsubscribe(ActionOnce),
                onDisposed
            );

            if (subscription == null)
            {
                return null;
            }

            try
            {
                subscription.Subscribe();
            }
            catch (Exception exception)
            {
                try
                {
                    Deregister(subscription);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"One-shot subscription registration failed. id: {subscription.Id}",
                        exception,
                        cleanupException);
                }

                throw;
            }

            return subscription;
        }

        public ISubscriptionInfo SubscribeOnce<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe, Action<T> callback)
            => SubscribeOnce(subscribe, unsubscribe, callback, null);

        public ISubscriptionInfo SubscribeOnce<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe, Action<T> callback, Action onDisposed)
        {
            if (subscribe == null || unsubscribe == null || callback == null)
            {
                return null;
            }

            ISubscriptionInfo subscription = null;
            int callbackInvoked = 0;

            void ActionOnce(T arg)
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                {
                    return;
                }

                EventExceptionUtility.InvokeAndAggregate(
                    () => Deregister(subscription),
                    () => callback(arg),
                    $"One-shot subscription failed. id: {subscription?.Id ?? 0}");
            }

            subscription = Register(
                () => subscribe(ActionOnce),
                () => unsubscribe(ActionOnce),
                onDisposed
            );

            if (subscription == null)
            {
                return null;
            }

            try
            {
                subscription.Subscribe();
            }
            catch (Exception exception)
            {
                try
                {
                    Deregister(subscription);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"One-shot subscription registration failed. id: {subscription.Id}",
                        exception,
                        cleanupException);
                }

                throw;
            }

            return subscription;
        }

        public ISubscriptionInfo SubscribeOnce<T1, T2>(Action<Action<T1, T2>> subscribe, Action<Action<T1, T2>> unsubscribe, Action<T1, T2> callback)
            => SubscribeOnce(subscribe, unsubscribe, callback, null);

        public ISubscriptionInfo SubscribeOnce<T1, T2>(Action<Action<T1, T2>> subscribe, Action<Action<T1, T2>> unsubscribe, Action<T1, T2> callback, Action onDisposed)
        {
            if (subscribe == null || unsubscribe == null || callback == null)
            {
                return null;
            }

            ISubscriptionInfo subscription = null;
            int callbackInvoked = 0;

            void ActionOnce(T1 arg1, T2 arg2)
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                {
                    return;
                }

                EventExceptionUtility.InvokeAndAggregate(
                    () => Deregister(subscription),
                    () => callback(arg1, arg2),
                    $"One-shot subscription failed. id: {subscription?.Id ?? 0}");
            }

            subscription = Register(
                () => subscribe(ActionOnce),
                () => unsubscribe(ActionOnce),
                onDisposed
            );

            if (subscription == null)
            {
                return null;
            }

            try
            {
                subscription.Subscribe();
            }
            catch (Exception exception)
            {
                try
                {
                    Deregister(subscription);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"One-shot subscription registration failed. id: {subscription.Id}",
                        exception,
                        cleanupException);
                }

                throw;
            }

            return subscription;
        }
    }
}
