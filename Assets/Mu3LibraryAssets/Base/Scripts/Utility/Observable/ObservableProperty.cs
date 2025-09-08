namespace Mu3Library.Base.Observable
{
    public abstract class ObservableProperty<T>
    {
        [UnityEngine.SerializeField] protected T _value;
        public T Value => _value;

        protected System.Action<T> _callback;



        #region Utility
        public void Set(T value)
        {
            if (!_value.Equals(value))
            {
                _value = value;
                _callback?.Invoke(value);
            }
        }

        public void SetWithoutEvent(T value)
        {
            _value = value;
        }

        public void AddEvent(System.Action<T> callback)
        {
            if (callback == null)
            {
                return;
            }
            _callback += callback;
        }

        public void RemoveEvent(System.Action<T> callback)
        {
            if (callback == null)
            {
                return;
            }
            _callback -= callback;
        }
        #endregion
    }
}
