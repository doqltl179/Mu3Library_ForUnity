using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mu3Library.Editor.Observable;
using Mu3Library.Observable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    public class ObservableTests
    {
        private const string RepeatedKeyJson =
            "{\"Dictionary\":{\"_entries\":[" +
            "{\"Key\":\"a\",\"Value\":1}," +
            "{\"Key\":\"a\",\"Value\":2}," +
            "{\"Key\":\"b\",\"Value\":3}]}}";

        [Serializable]
        private sealed class DictionaryHolder
        {
            public ObservableDictionary<string, int> Dictionary = new();
        }

        [Serializable]
        private sealed class FloatHolder
        {
            public ObservableFloat Observable = new();
        }

        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object created in _createdObjects)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _createdObjects.Clear();
        }

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

        [Test]
        public void ObservableFloat_SerializedLayout_KeepsValueField()
        {
            FloatHolder holder = JsonUtility.FromJson<FloatHolder>("{\"Observable\":{\"_value\":1.5}}");

            Assert.AreEqual(1.5f, holder.Observable.Value);
            StringAssert.Contains("\"Observable\":{\"_value\":1.5}", JsonUtility.ToJson(holder));
        }

        [Test]
        public void ObservableList_New_ValueIsEmptyList()
        {
            ObservableList<int> list = new();

            Assert.IsNotNull(list.Value);
            Assert.IsNotNull(list.ReadOnly.Value);
            Assert.AreEqual(0, list.Value.Count);
        }

        [Test]
        public void ObservableList_SubscribeNotifyImmediately_ReceivesEmptyList()
        {
            ObservableList<int> list = new();
            int receivedCount = -1;

            list.Subscribe(value => receivedCount = value.Count, notifyImmediately: true);

            Assert.AreEqual(0, receivedCount);
        }

        [Test]
        public void Subscribe_NotifyImmediatelyThrows_RethrowsAndLeavesNoSubscription()
        {
            ObservableInt observable = new();
            int throwingCalls = 0;
            int earlierCalls = 0;
            observable.Subscribe(_ => earlierCalls++);

            Assert.Throws<InvalidOperationException>(() => observable.Subscribe(_ =>
            {
                throwingCalls++;
                throw new InvalidOperationException("boom");
            }, notifyImmediately: true));

            observable.Set(1);

            Assert.AreEqual(1, throwingCalls);
            Assert.AreEqual(1, earlierCalls);
        }

        [Test]
        public void ObservableDictionary_SubscribeNotifyImmediatelyThrows_RethrowsAndLeavesNoSubscription()
        {
            ObservableDictionary<string, int> dictionary = new();
            int throwingCalls = 0;
            int earlierCalls = 0;
            dictionary.Subscribe(_ => earlierCalls++);

            Assert.Throws<InvalidOperationException>(() => dictionary.Subscribe(_ =>
            {
                throwingCalls++;
                throw new InvalidOperationException("boom");
            }, notifyImmediately: true));

            dictionary["a"] = 1;

            Assert.AreEqual(1, throwingCalls);
            Assert.AreEqual(1, earlierCalls);
        }

        [Test]
        public void ObservableDictionary_SerializationRoundTrip_KeepsEntries()
        {
            DictionaryHolder source = new();
            source.Dictionary.Add("a", 1);
            source.Dictionary.Add("b", 2);

            source.Dictionary.OnBeforeSerialize();
            source.Dictionary.OnAfterDeserialize();
            DictionaryHolder copy = JsonUtility.FromJson<DictionaryHolder>(JsonUtility.ToJson(source));

            Assert.AreEqual(2, source.Dictionary.Count);
            Assert.AreEqual(1, source.Dictionary["a"]);
            Assert.AreEqual(2, source.Dictionary["b"]);
            Assert.AreEqual(2, copy.Dictionary.Count);
            Assert.AreEqual(1, copy.Dictionary["a"]);
            Assert.AreEqual(2, copy.Dictionary["b"]);
        }

        [Test]
        public void ObservableDictionary_RepeatedKeyRows_FirstRowWinsAndRowsSurvive()
        {
            DictionaryHolder holder = JsonUtility.FromJson<DictionaryHolder>(RepeatedKeyJson);

            Assert.AreEqual(2, holder.Dictionary.Count);
            Assert.AreEqual(1, holder.Dictionary["a"]);
            Assert.AreEqual(3, holder.Dictionary["b"]);
            Assert.AreEqual(3, CountRows(JsonUtility.ToJson(holder)));
        }

        [Test]
        public void ObservableDictionary_RuntimeAdd_AppendsRowAndKeepsRepeatedRows()
        {
            DictionaryHolder holder = JsonUtility.FromJson<DictionaryHolder>(RepeatedKeyJson);

            holder.Dictionary.Add("c", 4);
            string json = JsonUtility.ToJson(holder);
            DictionaryHolder copy = JsonUtility.FromJson<DictionaryHolder>(json);

            Assert.AreEqual(4, CountRows(json));
            Assert.AreEqual(3, copy.Dictionary.Count);
            Assert.AreEqual(1, copy.Dictionary["a"]);
            Assert.AreEqual(3, copy.Dictionary["b"]);
            Assert.AreEqual(4, copy.Dictionary["c"]);
        }

        [Test]
        public void ObservableDictionary_RuntimeSetAndRemove_UpdateFirstRowAndDropRemovedKeys()
        {
            DictionaryHolder holder = JsonUtility.FromJson<DictionaryHolder>(RepeatedKeyJson);

            holder.Dictionary["a"] = 5;
            holder.Dictionary.Remove("b");
            string json = JsonUtility.ToJson(holder);
            DictionaryHolder copy = JsonUtility.FromJson<DictionaryHolder>(json);

            Assert.AreEqual(2, CountRows(json));
            Assert.AreEqual(1, copy.Dictionary.Count);
            Assert.AreEqual(5, copy.Dictionary["a"]);

            holder.Dictionary.Remove("a");

            Assert.AreEqual(0, CountRows(JsonUtility.ToJson(holder)));
        }

        [Test]
        public void ObservableDictionary_InspectorGrowsList_RepeatedRowNeitherOverwritesNorDisappears()
        {
            ObservableTestAsset asset = CreateAsset();
            asset.Dictionary.Add("a", 1);
            asset.Dictionary.Add("b", 2);
            SerializedObject serializedObject = new(asset);
            SerializedProperty entries = serializedObject.FindProperty("_dictionary._entries");

            // The list's "+" button grows the array; the new row starts with a key already in use.
            entries.arraySize++;
            SerializedProperty newRow = entries.GetArrayElementAtIndex(2);
            newRow.FindPropertyRelative("Key").stringValue = "a";
            newRow.FindPropertyRelative("Value").intValue = 9;
            serializedObject.ApplyModifiedProperties();

            Assert.AreEqual(2, asset.Dictionary.Count);
            Assert.AreEqual(1, asset.Dictionary["a"]);
            Assert.AreEqual(2, asset.Dictionary["b"]);

            serializedObject.Update();
            Assert.AreEqual(3, entries.arraySize);
            StringAssert.Contains("Element 2 has the same key as element 0.", ObservableDictionaryDrawer.DescribeIgnoredRows(entries));

            newRow = entries.GetArrayElementAtIndex(2);
            newRow.FindPropertyRelative("Key").stringValue = "c";
            serializedObject.ApplyModifiedProperties();

            Assert.AreEqual(3, asset.Dictionary.Count);
            Assert.AreEqual(1, asset.Dictionary["a"]);
            Assert.AreEqual(9, asset.Dictionary["c"]);
            Assert.IsNull(ObservableDictionaryDrawer.DescribeIgnoredRows(entries));
        }

        [Test]
        public void ObservableDictionary_RuntimeAddWhileInspected_ShowsRowWithItsKeyAndValue()
        {
            ObservableTestAsset asset = CreateAsset();
            asset.Dictionary.Add("a", 1);
            SerializedObject serializedObject = new(asset);
            SerializedProperty entries = serializedObject.FindProperty("_dictionary._entries");
            Assert.AreEqual(1, entries.arraySize);

            asset.Dictionary.Add("hp", 10);
            serializedObject.Update();

            Assert.AreEqual(2, entries.arraySize);
            SerializedProperty addedRow = entries.GetArrayElementAtIndex(1);
            Assert.AreEqual("hp", addedRow.FindPropertyRelative("Key").stringValue);
            Assert.AreEqual(10, addedRow.FindPropertyRelative("Value").intValue);
        }

        [Test]
        public void DrawerTargets_NestedField_AppliesValueAndNotifies()
        {
            ObservableTestAsset asset = CreateAsset();
            ObservableInt health = asset.Nested.Health;
            int received = -1;
            health.Subscribe(value => received = value);
            SerializedObject serializedObject = new(asset);
            SerializedProperty valueProp = serializedObject.FindProperty("_nested._health._value");

            valueProp.intValue = 7;
            ObservableDrawerUtility.ApplyAndNotify(valueProp, 7);

            Assert.AreSame(health, asset.Nested.Health);
            Assert.AreEqual(7, health.Value);
            Assert.AreEqual(7, received);
        }

        [TestCase("_slotArray")]
        [TestCase("_slotList")]
        public void DrawerTargets_ElementField_AppliesValueAndNotifiesThatElement(string collectionField)
        {
            ObservableTestAsset asset = CreateAsset();
            bool isArray = collectionField == "_slotArray";
            ObservableInt first = isArray ? asset.SlotArray[0].Health : asset.SlotList[0].Health;
            ObservableInt second = isArray ? asset.SlotArray[1].Health : asset.SlotList[1].Health;
            int firstCalls = 0;
            int received = -1;
            first.Subscribe(_ => firstCalls++);
            second.Subscribe(value => received = value);
            SerializedObject serializedObject = new(asset);
            SerializedProperty valueProp = serializedObject.FindProperty($"{collectionField}.Array.data[1]._health._value");

            valueProp.intValue = 4;
            ObservableDrawerUtility.ApplyAndNotify(valueProp, 4);

            Assert.AreEqual(4, second.Value);
            Assert.AreEqual(4, received);
            Assert.AreEqual(0, first.Value);
            Assert.AreEqual(0, firstCalls);
        }

        [Test]
        public void DrawerTargets_MultipleObjects_NotifiesEveryObjectTheEditChanged()
        {
            ObservableTestAsset changed = CreateAsset();
            ObservableTestAsset unchanged = CreateAsset();
            unchanged.Direct.SetWithoutEvent(2f);
            int changedCalls = 0;
            int unchangedCalls = 0;
            changed.Direct.Subscribe(_ => changedCalls++);
            unchanged.Direct.Subscribe(_ => unchangedCalls++);
            SerializedObject serializedObject = new(new UnityEngine.Object[] { changed, unchanged });
            SerializedProperty valueProp = serializedObject.FindProperty("_direct._value");

            valueProp.floatValue = 2f;
            ObservableDrawerUtility.ApplyAndNotify(valueProp, 2f);

            Assert.AreEqual(2f, changed.Direct.Value);
            Assert.AreEqual(2f, unchanged.Direct.Value);
            Assert.AreEqual(1, changedCalls);
            Assert.AreEqual(0, unchangedCalls);
        }

        [Test]
        public void DrawerTargets_NestedListAndDictionary_NotifyAfterInspectorEdit()
        {
            ObservableTestAsset asset = CreateAsset();
            List<int> receivedItems = null;
            IReadOnlyDictionary<string, int> receivedStats = null;
            asset.Nested.Items.Subscribe(value => receivedItems = value);
            asset.Nested.Stats.Subscribe(value => receivedStats = value);
            SerializedObject serializedObject = new(asset);
            SerializedProperty items = serializedObject.FindProperty("_nested._items");
            SerializedProperty stats = serializedObject.FindProperty("_nested._stats");

            SerializedProperty itemValues = items.FindPropertyRelative("_value");
            itemValues.arraySize = 1;
            itemValues.GetArrayElementAtIndex(0).intValue = 5;
            SerializedProperty statRows = stats.FindPropertyRelative("_entries");
            statRows.arraySize = 1;
            statRows.GetArrayElementAtIndex(0).FindPropertyRelative("Key").stringValue = "hp";
            statRows.GetArrayElementAtIndex(0).FindPropertyRelative("Value").intValue = 10;
            serializedObject.ApplyModifiedProperties();
            ObservableDrawerUtility.InvokeOnTargets(items, nameof(ObservableList<int>.Notify));
            ObservableDrawerUtility.InvokeOnTargets(stats, nameof(ObservableDictionary<string, int>.RefreshFromSerialized));

            CollectionAssert.AreEqual(new[] { 5 }, receivedItems);
            Assert.IsNotNull(receivedStats);
            Assert.AreEqual(10, receivedStats["hp"]);
        }

        [Test]
        public void DrawerTargets_PathThatDoesNotResolve_FindsNothing()
        {
            ObservableTestAsset asset = CreateAsset();
            SerializedObject serializedObject = new(asset);

            Assert.IsEmpty(ObservableDrawerUtility.FindTargets<object>(serializedObject, "_nested._missing"));
            Assert.IsEmpty(ObservableDrawerUtility.FindTargets<object>(serializedObject, "_slotArray.Array.data[5]._health"));
            Assert.IsEmpty(ObservableDrawerUtility.FindTargets<ObservableInt>(serializedObject, "_direct"));
        }

        private ObservableTestAsset CreateAsset()
        {
            ObservableTestAsset asset = ScriptableObject.CreateInstance<ObservableTestAsset>();
            _createdObjects.Add(asset);
            return asset;
        }

        private static int CountRows(string json) => Regex.Matches(json, "\"Key\"").Count;
    }
}
