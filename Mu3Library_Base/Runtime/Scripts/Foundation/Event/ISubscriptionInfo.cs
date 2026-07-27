using System;

namespace Mu3Library.Foundation.Event
{
    public interface ISubscriptionInfo : IDisposable
    {
        public uint Id { get; }
        public bool IsSubscribed { get; }
        public bool IsDisposed { get; }



        public void Subscribe();
        public void Unsubscribe();
    }
}