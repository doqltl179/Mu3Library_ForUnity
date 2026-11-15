using System;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// What the simulated network holds back, taken out in the order it falls due: by time, then
    /// in the order it went in. A binary heap over one array, so holding a packet back allocates
    /// nothing once the array has grown.
    /// </summary>
    internal sealed class DelayQueue<T> where T : struct
    {
        private struct Entry
        {
            public double Due;
            public long Sequence;
            public T Item;
        }

        private Entry[] _entries = new Entry[16];
        private int _count;
        private long _nextSequence;

        public int Count => _count;

        /// <summary>The number the next item gets; every item that went in before has a smaller one.</summary>
        public long NextSequence => _nextSequence;

        public void Push(double due, T item)
        {
            if (_count == _entries.Length)
            {
                Array.Resize(ref _entries, _count * 2);
            }

            Entry entry = new Entry { Due = due, Sequence = _nextSequence++, Item = item };
            int index = _count++;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsBefore(entry, _entries[parent]))
                {
                    break;
                }

                _entries[index] = _entries[parent];
                index = parent;
            }

            _entries[index] = entry;
        }

        public bool TryPeek(out double due, out long sequence)
        {
            if (_count == 0)
            {
                due = 0.0;
                sequence = 0;
                return false;
            }

            due = _entries[0].Due;
            sequence = _entries[0].Sequence;
            return true;
        }

        public T Pop()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("The queue is empty.");
            }

            T item = _entries[0].Item;
            Entry last = _entries[--_count];

            // The vacated slot lets go of what its item referenced, such as a packet buffer.
            _entries[_count] = default;

            if (_count > 0)
            {
                int index = 0;
                while (true)
                {
                    int child = index * 2 + 1;
                    if (child >= _count)
                    {
                        break;
                    }

                    if (child + 1 < _count && IsBefore(_entries[child + 1], _entries[child]))
                    {
                        child++;
                    }

                    if (!IsBefore(_entries[child], last))
                    {
                        break;
                    }

                    _entries[index] = _entries[child];
                    index = child;
                }

                _entries[index] = last;
            }

            return item;
        }

        private static bool IsBefore(in Entry a, in Entry b)
        {
            return a.Due < b.Due || (a.Due == b.Due && a.Sequence < b.Sequence);
        }
    }
}
