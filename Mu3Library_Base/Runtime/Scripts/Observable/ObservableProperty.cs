namespace Mu3Library.Observable
{
    [System.Serializable]
    public abstract class ObservableProperty<T> :
        IObservableValue<T>,
        UnityEngine.ISerializationCallbackReceiver,
        IRestorableObservable
    {
        private static readonly bool s_canCompareRestoredValue = RestoredValueComparison.CanCompare(typeof(T));

        [UnityEngine.SerializeField] protected T _value;
        public T Value => _value;
        public IObservableValue<T> ReadOnly => this;

        protected System.Action<T> _callback;

        // The value the subscribers were last given or a method here last set. An undo, a redo, or
        // a prefab revert deserializes another value into _value without going through them.
        private T _settledValue;
        private bool _hasSettledValue;
        private bool _isRestored;



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

        public void SetWithoutEvent(T value)
        {
            _value = value;
            Settle();
        }

        public void Notify() => InvokeCallbacks(_value);

        /// <summary>
        /// Invokes every subscriber on its own, so one that throws is reported and does not
        /// keep the change from reaching the others.
        /// </summary>
        private void InvokeCallbacks(T value)
        {
            Settle();

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

        private void Settle()
        {
            _settledValue = _value;
            _hasSettledValue = true;
            _isRestored = false;
        }

        void UnityEngine.ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>
        /// Only marks the restore: this can run on a loading thread, where the subscribers must not
        /// be called. The first deserialization of an instance that nothing set yet loads it and
        /// restores nothing.
        /// </summary>
        void UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_hasSettledValue)
            {
                _isRestored = true;
                return;
            }

            Settle();
        }

        bool IRestorableObservable.NotifyIfRestored()
        {
            if (!_isRestored)
            {
                return false;
            }

            _isRestored = false;
            if (s_canCompareRestoredValue &&
                System.Collections.Generic.EqualityComparer<T>.Default.Equals(_value, _settledValue))
            {
                return false;
            }

            Notify();
            return true;
        }
    }
}
