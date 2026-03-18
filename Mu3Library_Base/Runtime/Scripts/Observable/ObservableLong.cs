namespace Mu3Library.Observable
{
    [System.Serializable]
    public class ObservableLong : ObservableProperty<long>
    {
        public static implicit operator long(ObservableLong observable)
            => observable != null ? observable.Value : default;

        public override string ToString()
            => _value.ToString();
    }
}

