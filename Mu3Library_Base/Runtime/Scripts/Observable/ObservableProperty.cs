namespace Mu3Library.Observable
{
    [System.Serializable]
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
            InvokeCallbacks(value);
        }

        public void SetWithoutEvent(T value) => _value = value;

        public void Notify() => InvokeCallbacks(_value);

        /// <summary>
        /// Invokes every subscriber on its own, so one that throws is reported and does not
        /// keep the change from reaching the others.
        /// </summary>
        private void InvokeCallbacks(T value)
        {
            System.Delegate[] listeners = _callback?.GetInvocationList();
            if (listeners == null)
            {
                return;
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((System.Action<T>)listeners[i]).Invoke(value);
                }
                catch (System.Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        public void AddEvent(System.Action<T> callback) => _callback += callback;
        public void RemoveEvent(System.Action<T> callback) => _callback -= callback;

        /// <summary>
        /// Registers the callback and returns the token that removes it. When
        /// <paramref name="notifyImmediately"/> is set and the first call throws, the callback is
        /// removed again before the exception leaves, since the caller never gets a token for it.
        /// </summary>
        public System.IDisposable Subscribe(System.Action<T> callback, bool notifyImmediately = false)
        {
            if (callback == null)
            {
                return null;
            }

            AddEvent(callback);
            if (notifyImmediately)
            {
                try
                {
                    callback.Invoke(_value);
                }
                catch
                {
                    RemoveEvent(callback);
                    throw;
                }
            }

            return new SubscriptionToken(() => RemoveEvent(callback));
        }
        #endregion
    }
}
