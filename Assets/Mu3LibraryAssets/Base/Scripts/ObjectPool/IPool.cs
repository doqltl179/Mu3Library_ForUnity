using System.Collections.Generic;
using System;

namespace Mu3Library.ObjectPool
{
    public interface IPool<T>
    {
        public Type ObjectType { get; }

        public void Add(T obj);
        public void AddAll(IEnumerable<T> objs);
        public T Get();
        public T[] GetAll();
    }
}