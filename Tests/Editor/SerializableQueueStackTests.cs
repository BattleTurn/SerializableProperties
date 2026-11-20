using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public class SerializableQueueStackTests
    {
        private class Holder : ScriptableObject
        {
            public SerializableQueue<int> queue = new();
            public SerializableStack<int> stack = new();
        }

        [Test]
        public void Queue_IsFifo()
        {
            var q = new SerializableQueue<int>();
            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);

            Assert.AreEqual(1, q.Peek());
            Assert.AreEqual(1, q.Dequeue());
            Assert.AreEqual(2, q.Dequeue());
            Assert.AreEqual(3, q.Dequeue());
            Assert.AreEqual(0, q.Count);
        }

        [Test]
        public void Stack_IsLifo()
        {
            var s = new SerializableStack<int>();
            s.Push(1);
            s.Push(2);
            s.Push(3);

            Assert.AreEqual(3, s.Peek());
            Assert.AreEqual(3, s.Pop());
            Assert.AreEqual(2, s.Pop());
            Assert.AreEqual(1, s.Pop());
            Assert.AreEqual(0, s.Count);
        }

        // Enumeration order is the easiest thing to get backwards, so pin both.
        [Test]
        public void Queue_EnumeratesFrontFirst_StackEnumeratesTopFirst()
        {
            var q = new SerializableQueue<int>();
            var s = new SerializableStack<int>();
            foreach (int i in new[] { 1, 2, 3 })
            {
                q.Enqueue(i);
                s.Push(i);
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, q.ToList());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, q.ToArray());
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, s.ToList());
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, s.ToArray());
        }

        [Test]
        public void ConvertedCopies_KeepOrder()
        {
            var q = new SerializableQueue<int>();
            q.Enqueue(1);
            q.Enqueue(2);
            Assert.AreEqual(1, q.ToQueue().Dequeue());

            var s = new SerializableStack<int>();
            s.Push(1);
            s.Push(2);
            Assert.AreEqual(2, s.ToStack().Pop());
        }

        [Test]
        public void Empty_Throws_AndTryVariantsReturnFalse()
        {
            var q = new SerializableQueue<int>();
            var s = new SerializableStack<int>();

            Assert.Throws<InvalidOperationException>(() => q.Dequeue());
            Assert.Throws<InvalidOperationException>(() => q.Peek());
            Assert.Throws<InvalidOperationException>(() => s.Pop());
            Assert.Throws<InvalidOperationException>(() => s.Peek());

            Assert.IsFalse(q.TryDequeue(out _));
            Assert.IsFalse(q.TryPeek(out _));
            Assert.IsFalse(s.TryPop(out _));
            Assert.IsFalse(s.TryPeek(out _));
        }

        // The whole point of backing these with a List: the values actually persist.
        [Test]
        public void SurvivesSerializationRoundTrip()
        {
            var source = ScriptableObject.CreateInstance<Holder>();
            try
            {
                source.queue.Enqueue(10);
                source.queue.Enqueue(20);
                source.stack.Push(30);
                source.stack.Push(40);

                string json = EditorJsonUtility.ToJson(source);

                var restored = ScriptableObject.CreateInstance<Holder>();
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(json, restored);

                    CollectionAssert.AreEqual(new[] { 10, 20 }, restored.queue.ToList());
                    CollectionAssert.AreEqual(new[] { 40, 30 }, restored.stack.ToList());
                }
                finally { UnityEngine.Object.DestroyImmediate(restored); }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
