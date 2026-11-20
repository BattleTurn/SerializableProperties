using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public class Serializable2DArrayTests
    {
        private class Holder : ScriptableObject
        {
            public Serializable2DArray<int> grid = new(3, 2);
        }

        // Both constructors used to leave the backing array null, so the first indexer
        // call threw NullReferenceException until Unity had serialized the object once.
        [Test]
        public void SizedConstructor_IsUsableImmediately()
        {
            var grid = new Serializable2DArray<int>(3, 2);

            Assert.AreEqual(3, grid.X);
            Assert.AreEqual(2, grid.Y);
            Assert.AreEqual(3, grid.GetLength(0));
            Assert.AreEqual(2, grid.GetLength(1));

            grid[2, 1] = 7;
            Assert.AreEqual(7, grid[2, 1]);
        }

        [Test]
        public void DefaultConstructor_IsUsableImmediately()
        {
            var grid = new Serializable2DArray<int>();

            Assert.AreEqual(1, grid.X);
            Assert.AreEqual(1, grid.Y);

            grid[0, 0] = 5;
            Assert.AreEqual(5, grid[0, 0]);
        }

        [Test]
        public void SizedConstructor_ClampsToAtLeastOne()
        {
            var grid = new Serializable2DArray<int>(0, -4);

            Assert.AreEqual(1, grid.X);
            Assert.AreEqual(1, grid.Y);
            Assert.AreEqual(0, grid[0, 0]);
        }

        // Growing Y changes the flat index of every cell, so a naive resize shuffles the grid.
        [Test]
        public void SetSize_Grow_KeepsEveryValueAtItsOwnCoordinate()
        {
            var grid = new Serializable2DArray<int>(2, 2);
            grid[0, 0] = 1;
            grid[1, 0] = 2;
            grid[0, 1] = 3;
            grid[1, 1] = 4;

            grid.SetSize(3, 4);

            Assert.AreEqual(1, grid[0, 0]);
            Assert.AreEqual(2, grid[1, 0]);
            Assert.AreEqual(3, grid[0, 1]);
            Assert.AreEqual(4, grid[1, 1]);
            Assert.AreEqual(0, grid[2, 3], "new cells start empty");
            Assert.AreEqual(3, grid.X);
            Assert.AreEqual(4, grid.Y);
        }

        [Test]
        public void SetSize_Shrink_KeepsTheOverlap()
        {
            var grid = new Serializable2DArray<int>(3, 3);
            grid[0, 0] = 1;
            grid[2, 2] = 9;

            grid.SetSize(2, 2);

            Assert.AreEqual(1, grid[0, 0]);
            Assert.AreEqual(2, grid.X);
            Assert.AreEqual(2, grid.Y);
        }

        // SetSize rebuilds the flat list, so a resize survives even if the next thing
        // that happens is a deserialize rather than a serialize.
        [Test]
        public void SetSize_ThenSerialize_KeepsValues()
        {
            var source = ScriptableObject.CreateInstance<Holder>();
            try
            {
                source.grid[0, 0] = 11;
                source.grid[2, 1] = 22;
                source.grid.SetSize(4, 3);

                string json = EditorJsonUtility.ToJson(source);

                var restored = ScriptableObject.CreateInstance<Holder>();
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(json, restored);

                    Assert.AreEqual(4, restored.grid.X);
                    Assert.AreEqual(3, restored.grid.Y);
                    Assert.AreEqual(11, restored.grid[0, 0]);
                    Assert.AreEqual(22, restored.grid[2, 1]);
                }
                finally { Object.DestroyImmediate(restored); }
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}
