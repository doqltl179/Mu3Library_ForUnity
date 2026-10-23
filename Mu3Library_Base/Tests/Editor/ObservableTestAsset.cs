using System;
using System.Collections.Generic;
using Mu3Library.Observable;
using UnityEngine;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Holds Observable fields directly, inside a serializable class, a struct, a managed
    /// reference, and array and list elements, so the tests can edit them through a
    /// <c>SerializedObject</c> as the inspector
    /// does. It lives in its own file because Unity serializes a ScriptableObject only through
    /// a script file named after it.
    /// </summary>
    public class ObservableTestAsset : ScriptableObject
    {
        [Serializable]
        public class Slot
        {
            [SerializeField] private ObservableInt _health = new ObservableInt();
            [SerializeField] private ObservableList<int> _items = new ObservableList<int>();
            [SerializeField] private ObservableDictionary<string, int> _stats = new ObservableDictionary<string, int>();

            public ObservableInt Health => _health;
            public ObservableList<int> Items => _items;
            public ObservableDictionary<string, int> Stats => _stats;
        }

        [Serializable]
        public struct StructSlot
        {
            [SerializeField] private ObservableInt _health;

            public StructSlot(ObservableInt health)
            {
                _health = health;
            }

            public ObservableInt Health => _health;
        }

        [SerializeField] private ObservableFloat _direct = new ObservableFloat();
        [SerializeField] private Slot _nested = new Slot();
        [SerializeField] private Slot[] _slotArray = { new Slot(), new Slot() };
        [SerializeField] private List<Slot> _slotList = new List<Slot> { new Slot(), new Slot() };
        [SerializeField] private StructSlot _structSlot = new StructSlot(new ObservableInt());
        [SerializeReference] private Slot _referencedSlot = new Slot();
        [SerializeField] private ObservableDictionary<string, int> _dictionary = new ObservableDictionary<string, int>();

        public ObservableFloat Direct => _direct;
        public Slot Nested => _nested;
        public Slot[] SlotArray => _slotArray;
        public List<Slot> SlotList => _slotList;
        public StructSlot InStruct => _structSlot;
        public Slot ReferencedSlot => _referencedSlot;
        public ObservableDictionary<string, int> Dictionary => _dictionary;
    }
}
