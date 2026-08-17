using System;
using System.Text.RegularExpressions;
using Mu3Library.Observable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    public class ObservableTests
    {
        [Test]
        public void Set_ChangedValue_NotifiesSubscriber()
        {
            ObservableInt observable = new();
            int received = -1;

            observable.Subscribe(value => received = value);
            observable.Set(5);

            Assert.AreEqual(5, received);
            Assert.AreEqual(5, observable.Value);
        }

        [Test]
        public void Set_SameValue_DoesNotNotify()
        {
            ObservableInt observable = new();
            observable.Set(5);

            int notifyCount = 0;
            observable.Subscribe(_ => notifyCount++);
            observable.Set(5);

            Assert.AreEqual(0, notifyCount);
        }

        [Test]
        public void SetWithoutEvent_DoesNotNotify()
        {
            ObservableInt observable = new();
            int notifyCount = 0;
            observable.Subscribe(_ => notifyCount++);

            observable.SetWithoutEvent(7);

            Assert.AreEqual(0, notifyCount);
            Assert.AreEqual(7, observable.Value);
        }

        [Test]
        public void Subscribe_TokenDispose_StopsNotifications()
        {
            ObservableInt observable = new();
            int notifyCount = 0;

            IDisposable token = observable.Subscribe(_ => notifyCount++);
            token.Dispose();
            observable.Set(1);

            Assert.AreEqual(0, notifyCount);
        }

        [Test]
        public void Notify_ThrowingSubscriber_DoesNotBlockOthers()
        {
            ObservableInt observable = new();
            int received = -1;

            observable.Subscribe(_ => throw new InvalidOperationException("boom"));
            observable.Subscribe(value => received = value);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            observable.Set(3);

            Assert.AreEqual(3, received);
        }

        [Test]
        public void ObservableList_AddAndRemove_Notify()
        {
            ObservableList<int> list = new();
            int notifyCount = 0;
            list.Subscribe(_ => notifyCount++);

            list.Add(1);
            list.Add(2);
            list.Remove(1);

            Assert.AreEqual(3, notifyCount);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void ObservableDictionary_SetAndRemove_Notify()
        {
            ObservableDictionary<string, int> dictionary = new();
            int notifyCount = 0;
            dictionary.Subscribe(_ => notifyCount++);

            dictionary["a"] = 1;
            dictionary["a"] = 1;
            dictionary.Remove("a");

            Assert.AreEqual(2, notifyCount);
            Assert.AreEqual(0, dictionary.Count);
        }
    }
}
