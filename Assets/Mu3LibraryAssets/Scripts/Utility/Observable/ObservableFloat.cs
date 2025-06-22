namespace Mu3Library.Utility
{
    public class ObservableFloat : ObservableProperty<float>
    {



        public ObservableFloat(float initValue = 0, System.Action<float> callback = null)
        {
            _value = initValue;
            _callback = callback;
        }
    }
}
