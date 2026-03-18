namespace Mu3Library.Observable
{
    [System.Serializable]
    public class ObservableInt : ObservableProperty<int>
    {
        public static implicit operator int(ObservableInt observable)
            => observable != null ? observable.Value : default;

        public override string ToString()
            => _value.ToString();
    }
}

