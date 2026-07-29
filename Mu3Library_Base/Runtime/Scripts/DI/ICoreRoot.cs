using System;
using Mu3Library.Foundation.Event;

namespace Mu3Library.DI
{
    public interface ICoreRoot
    {
        public T GetClass<TCore, T>()
            where TCore : CoreBase
            where T : class;

        public object GetClass(Type coreType, Type serviceType, string key);

        public bool HasCore<T>() where T : CoreBase;

        public event Action<Type> OnCoreInitialized;
        public event Action<Type> OnCorePrepared;



        public ISubscriptionInfo SubscribeOnCoreInitializedOnce<T>(Action callback) where T : CoreBase;
        public ISubscriptionInfo SubscribeOnCoreInitializedOnce(Type type, Action callback);

        public ISubscriptionInfo SubscribeOnCorePreparedOnce<T>(Action callback) where T : CoreBase;
        public ISubscriptionInfo SubscribeOnCorePreparedOnce(Type type, Action callback);
    }
}