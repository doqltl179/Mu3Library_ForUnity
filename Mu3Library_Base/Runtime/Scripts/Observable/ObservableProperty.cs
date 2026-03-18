namespace Mu3Library.Observable
{
    public abstract class ObservableProperty<T> : IObservableValue<T>
    {
        [UnityEngine.SerializeField] protected T _value;
        public T Value => _value;
        public IObservableValue<T> ReadOnly => this;

        protected System.Action<T> _callback;



        #region Utility
        public void Set(T value)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            _value = value;
            _callback?.Invoke(value);
        }

        public void SetWithoutEvent(T value) => _value = value;

        public void Notify() => _callback?.Invoke(_value);

        public void AddEvent(System.Action<T> callback) => _callback += callback;
        public void RemoveEvent(System.Action<T> callback) => _callback -= callback;

        public System.IDisposable Subscribe(System.Action<T> callback, bool notifyImmediately = false)
        {
            if (callback == null)
            {
                return null;
            }

            AddEvent(callback);
            if (notifyImmediately)
            {
                callback.Invoke(_value);
            }

            return new SubscriptionToken(() => RemoveEvent(callback));
        }
        #endregion
    }
}
