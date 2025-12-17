namespace Mu3Library.Observable
{
    [System.Serializable]
    public class ObservableFloat : ObservableProperty<float>
    {
        public static implicit operator float(ObservableFloat observable) => observable.Value;

        public override string ToString() => _value.ToString();
    }
}
