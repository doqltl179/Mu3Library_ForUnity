using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Observable
{
    [Serializable]
    public class ObservableDictionary<TKey, TValue> :
        IObservableValue<IReadOnlyDictionary<TKey, TValue>>,
        ISerializationCallbackReceiver,
        IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>,
        IRestorableObservable
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
        private HashSet<TKey> _seenKeys;

        // The last dictionary built before Unity deserialized the rows again (an undo, a redo, or a
        // prefab revert restores them), kept until the subscribers are notified. Null when nothing
        // built one since the rows were loaded.
        private Dictionary<TKey, TValue> _dictionaryBeforeRestore;
        private bool _isLoaded;
        private bool _isRestored;

        private Dictionary<TKey, TValue> _dictionaryValue
        {
            get
            {
                EnsureDictionary();
                return _dictionary;
            }
        }

        public IReadOnlyDictionary<TKey, TValue> Value => _dictionaryValue;
        public IObservableValue<IReadOnlyDictionary<TKey, TValue>> ReadOnly => this;

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

            ReplaceContents(value);
            Notify();
        }

        public void SetWithoutEvent(Dictionary<TKey, TValue> value)
        {
            EnsureDictionary();

            ReplaceContents(value);
        }

        private void ReplaceContents(Dictionary<TKey, TValue> value)
        {
            _dictionary.Clear();
            if (value != null)
            {
                foreach (KeyValuePair<TKey, TValue> pair in value)
                {
                    _dictionary[pair.Key] = pair.Value;
                }
            }
        }

        public void Notify() => InvokeCallbacks(_dictionaryValue);

        /// <summary>
        /// Invokes every subscriber on its own, so one that throws is reported and does not
        /// keep the change from reaching the others.
        /// </summary>
        private void InvokeCallbacks(IReadOnlyDictionary<TKey, TValue> value)
        {
            _isRestored = false;
            _dictionaryBeforeRestore = null;

            Delegate[] listeners = _callback?.GetInvocationList();
            if (listeners == null)
            {
                return;
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action<IReadOnlyDictionary<TKey, TValue>>)listeners[i]).Invoke(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public void AddEvent(Action<IReadOnlyDictionary<TKey, TValue>> callback) => _callback += callback;
        public void RemoveEvent(Action<IReadOnlyDictionary<TKey, TValue>> callback) => _callback -= callback;

        /// <summary>
        /// Registers the callback and returns the token that removes it. When
        /// <paramref name="notifyImmediately"/> is set and the first call throws, the callback is
        /// removed again before the exception leaves, since the caller never gets a token for it.
        /// </summary>
        public IDisposable Subscribe(
            Action<IReadOnlyDictionary<TKey, TValue>> callback,
            bool notifyImmediately = false)
        {
            if (callback == null)
            {
                return null;
            }

            AddEvent(callback);
            if (notifyImmediately)
            {
                try
                {
                    callback.Invoke(_dictionaryValue);
                }
                catch
                {
                    RemoveEvent(callback);
                    throw;
                }
            }

            return new SubscriptionToken(() => RemoveEvent(callback));
        }

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

        /// <summary>
        /// Writes runtime changes back to the serialized rows. Rows the dictionary does not hold,
        /// a repeated key or a missing one, are kept: they are rows someone is still editing in
        /// the inspector, and rewriting the list from the dictionary would delete them.
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (_dictionary == null)
            {
                // Nothing built the dictionary since the rows were loaded, so they are current.
                return;
            }

            if (_entries == null)
            {
                _entries = new List<Entry>(_dictionary.Count);
            }

            WriteDictionaryToEntries();
        }

        /// <summary>
        /// Drops the dictionary so it is built again from the deserialized rows, and only marks
        /// the restore: this can run on a loading thread, where the subscribers must not be called.
        /// The first deserialization loads the rows and restores nothing.
        /// </summary>
        public void OnAfterDeserialize()
        {
            _isRestored = _isLoaded;
            _isLoaded = true;
            if (_dictionary != null)
            {
                _dictionaryBeforeRestore = _dictionary;
            }

            _dictionary = null;
        }

        bool IRestorableObservable.NotifyIfRestored()
        {
            if (!_isRestored)
            {
                return false;
            }

            Dictionary<TKey, TValue> before = _dictionaryBeforeRestore;
            _isRestored = false;
            _dictionaryBeforeRestore = null;
            if (before != null && HasSameEntries(before))
            {
                return false;
            }

            Notify();
            return true;
        }

        /// <summary>
        /// Compares the entries by the rule the indexer notifies by, so a restore notifies when a
        /// set would have.
        /// </summary>
        private bool HasSameEntries(Dictionary<TKey, TValue> other)
        {
            EnsureDictionary();
            if (_dictionary.Count != other.Count)
            {
                return false;
            }

            foreach (KeyValuePair<TKey, TValue> pair in _dictionary)
            {
                if (!other.TryGetValue(pair.Key, out TValue value) ||
                    !EqualityComparer<TValue>.Default.Equals(pair.Value, value))
                {
                    return false;
                }
            }

            return true;
        }

        public void RefreshFromSerialized()
        {
            _dictionary = null;
            Notify();
        }

        /// <summary>
        /// Builds the dictionary from the serialized rows. The first row with a key wins; a later
        /// row with the same key and a row without a key stay in the list but not in the
        /// dictionary, so a row added in the inspector never overwrites an existing entry.
        /// </summary>
        private void EnsureDictionary()
        {
            if (_dictionary != null)
            {
                return;
            }

            int entryCount = _entries?.Count ?? 0;
            _dictionary = new Dictionary<TKey, TValue>(entryCount);
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

                _dictionary.TryAdd(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Updates the rows in place: the first row of a key takes the dictionary's value or
        /// leaves with its key, repeated rows leave with it too, rows without a key stay, and
        /// keys the dictionary gained are appended. Rows that already describe the dictionary
        /// come out unchanged, so this runs on every serialization instead of first comparing
        /// values, which an <c>Equals</c> that ignores some fields would answer wrongly.
        /// </summary>
        private void WriteDictionaryToEntries()
        {
            if (_seenKeys == null)
            {
                _seenKeys = new HashSet<TKey>();
            }

            HashSet<TKey> seenKeys = _seenKeys;
            int writeIndex = 0;

            for (int readIndex = 0; readIndex < _entries.Count; readIndex++)
            {
                Entry entry = _entries[readIndex];
                if (entry.Key != null)
                {
                    bool isFirstRow = seenKeys.Add(entry.Key);
                    if (!_dictionary.TryGetValue(entry.Key, out TValue value))
                    {
                        continue;
                    }

                    if (isFirstRow)
                    {
                        entry.Value = value;
                    }
                }

                _entries[writeIndex++] = entry;
            }

            _entries.RemoveRange(writeIndex, _entries.Count - writeIndex);

            foreach (KeyValuePair<TKey, TValue> pair in _dictionary)
            {
                if (!seenKeys.Contains(pair.Key))
                {
                    _entries.Add(new Entry { Key = pair.Key, Value = pair.Value });
                }
            }

            // Release the keys, which may be objects the dictionary no longer holds.
            seenKeys.Clear();
        }
    }
}
