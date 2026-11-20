using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    /// <summary>
    /// FIFO queue Unity can serialize and draw. The List is the only copy of the data,
    /// so what the Inspector shows is what gets saved - no sync step to go wrong.
    /// </summary>
    [Serializable]
    public class SerializableQueue<T> : IReadOnlyCollection<T>
    {
        // Front of the queue is index 0, so the Inspector reads top-down in dequeue order.
        [SerializeField] private List<T> items = new();

        public int Count => items.Count;

        public void Enqueue(T item) => items.Add(item);

        // shortcut: Dequeue is O(n) because the list shifts; swap the backing store for a
        // ring buffer if this ever runs over thousands of items per frame.
        public T Dequeue()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("SerializableQueue is empty.");

            T head = items[0];
            items.RemoveAt(0);
            return head;
        }

        public bool TryDequeue(out T result)
        {
            if (items.Count == 0)
            {
                result = default;
                return false;
            }

            result = items[0];
            items.RemoveAt(0);
            return true;
        }

        public T Peek()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("SerializableQueue is empty.");

            return items[0];
        }

        public bool TryPeek(out T result)
        {
            if (items.Count == 0)
            {
                result = default;
                return false;
            }

            result = items[0];
            return true;
        }

        public void Clear() => items.Clear();

        public bool Contains(T item) => items.Contains(item);

        public T[] ToArray() => items.ToArray();

        /// <summary>Copy into a plain Queue for code that needs that exact type. The copy does not stay in sync.</summary>
        public Queue<T> ToQueue() => new Queue<T>(items);

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
