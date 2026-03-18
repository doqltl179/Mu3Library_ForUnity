using System;
using System.Collections;
using System.Collections.Generic;

namespace Mu3Library.Observable
{
    [Serializable]
    public class ObservableList<T> : ObservableProperty<List<T>>, IList<T>, IReadOnlyList<T>
    {
        private List<T> _list
        {
            get
            {
                if (_value == null)
                {
                    _value = new List<T>();
                }

                return _value;
            }
        }

        public int Count => _list.Count;
        public bool IsReadOnly => false;

        public T this[int index]
        {
            get => _list[index];
            set
            {
                if (EqualityComparer<T>.Default.Equals(_list[index], value))
                {
                    return;
                }

                _list[index] = value;
                Notify();
            }
        }

        public void Add(T item)
        {
            _list.Add(item);
            Notify();
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items is ICollection<T> collection)
            {
                if (collection.Count == 0)
                {
                    return;
                }

                _list.AddRange(collection);
                Notify();
                return;
            }

            bool addedAny = false;
            foreach (T item in items)
            {
                _list.Add(item);
                addedAny = true;
            }

            if (addedAny)
            {
                Notify();
            }
        }

        public void Clear()
        {
            if (_list.Count == 0)
            {
                return;
            }

            _list.Clear();
            Notify();
        }

        public bool Contains(T item) => _list.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        public int IndexOf(T item) => _list.IndexOf(item);

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            Notify();
        }

        public bool Remove(T item)
        {
            bool removed = _list.Remove(item);
            if (removed)
            {
                Notify();
            }

            return removed;
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
            Notify();
        }

        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }
}
