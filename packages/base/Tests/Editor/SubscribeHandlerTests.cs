using System;
using Mu3Library.Foundation.Event;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class SubscribeHandlerTests
    {
        private event Action TestEvent;

        private SubscribeHandler _handler;



        [SetUp]
        public void SetUp()
        {
            TestEvent = null;
            _handler = new SubscribeHandler();
        }

        [TearDown]
        public void TearDown()
        {
            _handler.Dispose();
        }

        [Test]
        public void Register_Subscribe_ReceivesEvent()
        {
            int received = 0;
            Action listener = () => received++;

            ISubscriptionInfo subscription = _handler.Register(
                () => TestEvent += listener,
                () => TestEvent -= listener);
            subscription.Subscribe();

            TestEvent?.Invoke();

            Assert.AreEqual(1, received);
        }

        [Test]
        public void Unsubscribe_StopsReceiving()
        {
            int received = 0;
            Action listener = () => received++;

            ISubscriptionInfo subscription = _handler.Register(
                () => TestEvent += listener,
                () => TestEvent -= listener);
            subscription.Subscribe();
            subscription.Unsubscribe();

            TestEvent?.Invoke();

            Assert.AreEqual(0, received);
        }

        [Test]
        public void SubscribeOnce_FiresExactlyOnce()
        {
            int received = 0;

            _handler.SubscribeOnce(
                h => TestEvent += h,
                h => TestEvent -= h,
                () => received++);

            TestEvent?.Invoke();
            TestEvent?.Invoke();

            Assert.AreEqual(1, received);
        }

        [Test]
        public void Deregister_RemovesSubscription()
        {
            int received = 0;
            Action listener = () => received++;

            ISubscriptionInfo subscription = _handler.Register(
                () => TestEvent += listener,
                () => TestEvent -= listener);
            subscription.Subscribe();

            _handler.Deregister(subscription);

            TestEvent?.Invoke();

            Assert.AreEqual(0, received);
            Assert.IsTrue(subscription.IsDisposed);
        }

        [Test]
        public void Dispose_UnsubscribesEverything()
        {
            int received = 0;
            Action listener = () => received++;

            ISubscriptionInfo subscription = _handler.Register(
                () => TestEvent += listener,
                () => TestEvent -= listener);
            subscription.Subscribe();

            _handler.Dispose();

            TestEvent?.Invoke();

            Assert.AreEqual(0, received);
            Assert.IsTrue(_handler.IsDisposed);
        }

        [Test]
        public void Register_AfterDispose_ReturnsNull()
        {
            _handler.Dispose();

            ISubscriptionInfo subscription = _handler.Register(() => { }, () => { });

            Assert.IsNull(subscription);
        }
    }
}
