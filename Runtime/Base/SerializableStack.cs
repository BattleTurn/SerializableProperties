using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    /// <summary>
    /// LIFO stack Unity can serialize and draw. The List is the only copy of the data,
    /// so what the Inspector shows is what gets saved - no sync step to go wrong.
    /// </summary>
    [Serializable]
    public class SerializableStack<T> : IReadOnlyCollection<T>
    {
        // Top of the stack is the LAST element, so Push/Pop are O(1) and the Inspector
        // reads bottom-up. Enumeration still yields top-first, matching Stack<T>.
        [SerializeField] private List<T> items = new();

        public int Count => items.Count;

        public void Push(T item) => items.Add(item);

        public T Pop()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("SerializableStack is empty.");

            int last = items.Count - 1;
            T top = items[last];
            items.RemoveAt(last);
            return top;
        }

        public bool TryPop(out T result)
        {
            if (items.Count == 0)
            {
                result = default;
                return false;
            }

            int last = items.Count - 1;
            result = items[last];
            items.RemoveAt(last);
            return true;
        }

        public T Peek()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("SerializableStack is empty.");

            return items[items.Count - 1];
        }

        public bool TryPeek(out T result)
        {
            if (items.Count == 0)
            {
                result = default;
                return false;
            }

            result = items[items.Count - 1];
            return true;
        }

        public void Clear() => items.Clear();

        public bool Contains(T item) => items.Contains(item);

        /// <summary>Top-first, like Stack&lt;T&gt;.ToArray().</summary>
        public T[] ToArray()
        {
            var copy = new T[items.Count];
            for (int i = 0; i < items.Count; i++)
                copy[i] = items[items.Count - 1 - i];
            return copy;
        }

        /// <summary>Copy into a plain Stack for code that needs that exact type. The copy does not stay in sync.</summary>
        public Stack<T> ToStack() => new Stack<T>(items);

        /// <summary>Top-first, like Stack&lt;T&gt;.</summary>
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = items.Count - 1; i >= 0; i--)
                yield return items[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
