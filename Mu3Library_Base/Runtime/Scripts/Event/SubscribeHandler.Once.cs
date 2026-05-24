using System;

namespace Mu3Library.Event
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

            void ActionOnce()
            {
                Deregister(id);
                callback();
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

            Subscribe(id);
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

            void ActionOnce(T arg)
            {
                Deregister(id);
                callback(arg);
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

            Subscribe(id);
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

            void ActionOnce(T1 arg1, T2 arg2)
            {
                Deregister(id);
                callback(arg1, arg2);
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

            Subscribe(id);
            return id;
        }
    }
}