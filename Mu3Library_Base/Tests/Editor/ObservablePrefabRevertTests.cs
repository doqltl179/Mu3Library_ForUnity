using System.Collections.Generic;
using Mu3Library.Editor.Observable;
using Mu3Library.Observable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The prefab revert path of the restore notification. A revert writes the prefab's values into
    /// the instance without going through its Observables, and the notifier delivers them from
    /// <c>PrefabUtility.prefabInstanceReverted</c>; undoing the revert comes back through
    /// <c>Undo.undoRedoPerformed</c>. Each test saves its prefab into a folder of its own under
    /// <c>Assets</c> and deletes the folder again.
    /// </summary>
    public class ObservablePrefabRevertTests
    {
        private const int PrefabHealth = 2;
        private const int PrefabMana = 3;
        private const int PrefabChildHealth = 4;
        private const int OverriddenHealth = 7;
        private const int OverriddenMana = 8;
        private const int OverriddenChildHealth = 9;

        private string _folderPath;
        private GameObject _instance;
        private ObservablePrefabTestComponent _root;
        private ObservablePrefabTestComponent _child;

        [SetUp]
        public void SetUp()
        {
            _folderPath = AssetDatabase.GUIDToAssetPath(
                AssetDatabase.CreateFolder("Assets", nameof(ObservablePrefabRevertTests)));

            GameObject source = new(nameof(ObservablePrefabRevertTests));
            try
            {
                ObservablePrefabTestComponent root = source.AddComponent<ObservablePrefabTestComponent>();
                root.Health.SetWithoutEvent(PrefabHealth);
                root.Mana.SetWithoutEvent(PrefabMana);
                GameObject child = new("Child");
                child.transform.SetParent(source.transform);
                child.AddComponent<ObservablePrefabTestComponent>().Health.SetWithoutEvent(PrefabChildHealth);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, _folderPath + "/Observables.prefab");
                _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }

            _root = _instance.GetComponent<ObservablePrefabTestComponent>();
            _child = _instance.transform.GetChild(0).GetComponent<ObservablePrefabTestComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }

            _instance = null;
            _root = null;
            _child = null;

            if (!string.IsNullOrEmpty(_folderPath))
            {
                AssetDatabase.DeleteAsset(_folderPath);
                _folderPath = null;
            }
        }

        [Test]
        public void RestoreNotifier_RevertPropertyOverride_NotifiesTheRevertedValue()
        {
            Undo.IncrementCurrentGroup();
            Override(_root, "_health", OverriddenHealth);
            Override(_root, "_mana", OverriddenMana);
            List<int> health = Record(_root.Health);
            List<int> mana = Record(_root.Mana);
            Selection.activeGameObject = _instance;

            Undo.IncrementCurrentGroup();
            using (SerializedObject serializedObject = new(_root))
            {
                PrefabUtility.RevertPropertyOverride(
                    serializedObject.FindProperty("_health._value"),
                    InteractionMode.UserAction);
            }

            Assert.AreEqual(PrefabHealth, _root.Health.Value);
            Assert.AreEqual(OverriddenMana, _root.Mana.Value);
            CollectionAssert.AreEqual(new[] { PrefabHealth }, health);

            Undo.PerformUndo();

            Assert.AreEqual(OverriddenHealth, _root.Health.Value);
            CollectionAssert.AreEqual(new[] { PrefabHealth, OverriddenHealth }, health);
            CollectionAssert.IsEmpty(mana, "An override the revert left in place is not notified.");
        }

        [Test]
        public void RestoreNotifier_RevertObjectOverride_NotifiesTheRevertedValue()
        {
            Undo.IncrementCurrentGroup();
            Override(_root, "_health", OverriddenHealth);
            Override(_child, "_health", OverriddenChildHealth);
            List<int> health = Record(_root.Health);
            List<int> mana = Record(_root.Mana);
            List<int> childHealth = Record(_child.Health);
            Selection.activeGameObject = _instance;

            Undo.IncrementCurrentGroup();
            PrefabUtility.RevertObjectOverride(_root, InteractionMode.UserAction);

            Assert.AreEqual(PrefabHealth, _root.Health.Value);
            Assert.AreEqual(OverriddenChildHealth, _child.Health.Value);
            CollectionAssert.AreEqual(new[] { PrefabHealth }, health);

            Undo.PerformUndo();

            Assert.AreEqual(OverriddenHealth, _root.Health.Value);
            CollectionAssert.AreEqual(new[] { PrefabHealth, OverriddenHealth }, health);
            CollectionAssert.IsEmpty(mana, "An Observable without an override is not notified.");
            CollectionAssert.IsEmpty(childHealth, "An override on another object is not notified.");
        }

        [Test]
        public void RestoreNotifier_RevertPrefabInstance_NotifiesTheRevertedValues()
        {
            Undo.IncrementCurrentGroup();
            Override(_root, "_health", OverriddenHealth);
            Override(_child, "_health", OverriddenChildHealth);
            List<int> health = Record(_root.Health);
            List<int> mana = Record(_root.Mana);
            List<int> childHealth = Record(_child.Health);
            Selection.objects = new Object[] { _instance, _child.gameObject };

            Undo.IncrementCurrentGroup();
            PrefabUtility.RevertPrefabInstance(_instance, InteractionMode.UserAction);

            Assert.AreEqual(PrefabHealth, _root.Health.Value);
            Assert.AreEqual(PrefabChildHealth, _child.Health.Value);
            CollectionAssert.AreEqual(new[] { PrefabHealth }, health);
            CollectionAssert.AreEqual(new[] { PrefabChildHealth }, childHealth);

            Undo.PerformUndo();

            Assert.AreEqual(OverriddenHealth, _root.Health.Value);
            Assert.AreEqual(OverriddenChildHealth, _child.Health.Value);
            CollectionAssert.AreEqual(new[] { PrefabHealth, OverriddenHealth }, health);
            CollectionAssert.AreEqual(new[] { PrefabChildHealth, OverriddenChildHealth }, childHealth);
            CollectionAssert.IsEmpty(mana, "An Observable without an override is not notified.");
        }

        // Changes the value as the Observable's inspector does, which makes it an override of the
        // instance.
        private static void Override(ObservablePrefabTestComponent component, string observablePath, int value)
        {
            using SerializedObject serializedObject = new(component);
            SerializedProperty valueProp = serializedObject.FindProperty(observablePath + "._value");
            valueProp.intValue = value;
            ObservableDrawerUtility.ApplyAndNotify(valueProp, value);
        }

        private static List<int> Record(ObservableInt observable)
        {
            List<int> received = new();
            observable.Subscribe(received.Add);
            return received;
        }
    }
}
