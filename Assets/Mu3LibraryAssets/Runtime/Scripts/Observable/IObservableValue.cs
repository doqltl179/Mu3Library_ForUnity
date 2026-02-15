namespace Mu3Library.Observable
{
    public interface IObservableValue<out TValue>
    {
        TValue Value { get; }

        System.IDisposable Subscribe(System.Action<TValue> callback, bool notifyImmediately = false);
    }
}
