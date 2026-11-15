namespace Mu3Library.Observable
{
    internal sealed class SubscriptionToken : System.IDisposable
    {
        private System.Action _unsubscribe;



        public SubscriptionToken(System.Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            System.Action unsubscribe = System.Threading.Interlocked.Exchange(ref _unsubscribe, null);

            unsubscribe?.Invoke();
        }
    }
}
