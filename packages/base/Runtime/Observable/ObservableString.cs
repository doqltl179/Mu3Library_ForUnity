namespace Mu3Library.Observable
{
    [System.Serializable]
    public class ObservableString : ObservableProperty<string>
    {
        public static implicit operator string(ObservableString observable)
            => observable != null ? observable.Value : default;

        public override string ToString()
            => _value ?? string.Empty;
    }
}

