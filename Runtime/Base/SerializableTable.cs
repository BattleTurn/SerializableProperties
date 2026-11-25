using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    /// <summary>
    /// A list drawn as a table: one row per element, one column per serialized field of T.
    /// This is a wrapper and not an attribute because Unity dispatches a PropertyAttribute
    /// drawer per array element, which can never see the whole collection.
    /// </summary>
    [Serializable]
    public class SerializableTable<T> : IReadOnlyList<T>
    {
        [SerializeField] private List<T> rows = new();

        /// <summary>The rows themselves. Mutate them directly; there is nothing else to keep in step.</summary>
        public List<T> Rows => rows;

        public int Count => rows.Count;

        public T this[int index]
        {
            get => rows[index];
            set => rows[index] = value;
        }

        public void Add(T row) => rows.Add(row);

        public void Insert(int index, T row) => rows.Insert(index, row);

        public bool Remove(T row) => rows.Remove(row);

        public void RemoveAt(int index) => rows.RemoveAt(index);

        public void Clear() => rows.Clear();

        public int IndexOf(T row) => rows.IndexOf(row);

        public IEnumerator<T> GetEnumerator() => rows.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
