using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Observable
{
    [Serializable]
    public class ObservableDictionary<TKey, TValue> :
        ISerializationCallbackReceiver,
        IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>
    {
        [Serializable]
        private struct Entry
        {
            public TKey Key;
            public TValue Value;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        private Dictionary<TKey, TValue> _dictionary;
        private Action<IReadOnlyDictionary<TKey, TValue>> _callback;

        private Dictionary<TKey, TValue> _dictionaryValue
        {
            get
            {
                EnsureDictionary();
                return _dictionary;
            }
        }

        public IReadOnlyDictionary<TKey, TValue> Value => _dictionaryValue;

        public TValue this[TKey key]
        {
            get => _dictionaryValue[key];
            set
            {
                EnsureDictionary();

                bool changed = !_dictionary.TryGetValue(key, out TValue oldValue) ||
                               !EqualityComparer<TValue>.Default.Equals(oldValue, value);

                _dictionary[key] = value;

                if (changed)
                {
                    Notify();
                }
            }
        }

        public ICollection<TKey> Keys => _dictionaryValue.Keys;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _dictionaryValue.Keys;

        public ICollection<TValue> Values => _dictionaryValue.Values;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _dictionaryValue.Values;

        public int Count => _dictionaryValue.Count;
        public bool IsReadOnly => false;

        public void Set(Dictionary<TKey, TValue> value)
        {
            EnsureDictionary();

            if (ReferenceEquals(_dictionary, value))
            {
                return;
            }

            _dictionary.Clear();
            if (value != null)
            {
                foreach (KeyValuePair<TKey, TValue> pair in value)
                {
                    _dictionary[pair.Key] = pair.Value;
                }
            }

            Notify();
        }

        public void SetWithoutEvent(Dictionary<TKey, TValue> value)
        {
            EnsureDictionary();

            _dictionary.Clear();
            if (value != null)
            {
                foreach (KeyValuePair<TKey, TValue> pair in value)
                {
                    _dictionary[pair.Key] = pair.Value;
                }
            }
        }

        public void Notify() => _callback?.Invoke(_dictionaryValue);

        public void AddEvent(Action<IReadOnlyDictionary<TKey, TValue>> callback) => _callback += callback;
        public void RemoveEvent(Action<IReadOnlyDictionary<TKey, TValue>> callback) => _callback -= callback;

        public void Add(TKey key, TValue value)
        {
            EnsureDictionary();
            _dictionary.Add(key, value);
            Notify();
        }

        public bool ContainsKey(TKey key) => _dictionaryValue.ContainsKey(key);

        public bool Remove(TKey key)
        {
            EnsureDictionary();
            bool removed = _dictionary.Remove(key);
            if (removed)
            {
                Notify();
            }

            return removed;
        }

        public bool TryGetValue(TKey key, out TValue value) => _dictionaryValue.TryGetValue(key, out value);

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            EnsureDictionary();
            if (_dictionary.Count == 0)
            {
                return;
            }

            _dictionary.Clear();
            Notify();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
            => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionaryValue).Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionaryValue).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            EnsureDictionary();
            bool removed = ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Remove(item);
            if (removed)
            {
                Notify();
            }

            return removed;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionaryValue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void OnBeforeSerialize()
        {
            EnsureDictionary();

            _entries.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in _dictionary)
            {
                _entries.Add(new Entry { Key = pair.Key, Value = pair.Value });
            }
        }

        public void OnAfterDeserialize()
        {
            _dictionary = null;
        }

        public void RefreshFromSerialized()
        {
            _dictionary = null;
            Notify();
        }

        private void EnsureDictionary()
        {
            if (_dictionary != null)
            {
                return;
            }

            _dictionary = new Dictionary<TKey, TValue>();
            if (_entries == null)
            {
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Key == null)
                {
                    continue;
                }

                _dictionary[entry.Key] = entry.Value;
            }
        }
    }
}
