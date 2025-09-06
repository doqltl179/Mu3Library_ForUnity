using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Mu3Library.Utility.ObjectPool
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