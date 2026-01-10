namespace Mu3Library.Observable
{
    [System.Serializable]
    public class ObservableBool : ObservableProperty<bool>
    {
        public static implicit operator bool(ObservableBool observable)
            => observable != null ? observable.Value : default;

        public override string ToString()
            => _value.ToString();
    }
}
