using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    public enum RowOrder
    {
        TopToBottom = 0,
        BottomToTop = 1
    }

    [Serializable]
    public class Serializable2DArray<T> : ISerializationCallbackReceiver
    {
        [SerializeField] private int y;
        [SerializeField] private int x;

        [SerializeField] private List<T> serializedValues = new();

        [SerializeField] private RowOrder rowOrder = RowOrder.TopToBottom;

        [NonSerialized] private T[,] array2D;

        public int Y => y;
        public int X => x;
        public RowOrder RowOrder => rowOrder;

        public Serializable2DArray()
        {
            y = 1;
            x = 1;
            SetSize(x, y);
        }

        public Serializable2DArray(int x, int y)
        {
            this.x = Mathf.Max(1, x);
            this.y = Mathf.Max(1, y);
            SetSize(x, y);
        }

        public void SetSize(int newX, int newY)
        {
            if (newY == y && newX == x)
                return;

            x = Mathf.Max(1, newX);
            y = Mathf.Max(1, newY);
            // Resize the 2D array
            T[,] newArray = new T[x, y];

            if (array2D != null)
            {
                int minR = Mathf.Min(x, array2D.GetLength(0));
                int minC = Mathf.Min(y, array2D.GetLength(1));

                // copy old data → new data
                for (int x = 0; x < minR; x++)
                    for (int y = 0; y < minC; y++)
                        newArray[x, y] = array2D[x, y];
            }

            array2D = newArray;
            serializedValues = new List<T>(y * x);
        }

        public T this[int x, int y]
        {
            get => array2D[x, y];
            set => array2D[x, y] = value;
        }

        public void OnBeforeSerialize()
        {
            serializedValues.Clear();
            if (array2D == null)
                array2D = new T[x, y];

            for (int r = 0; r < x; r++)
                for (int c = 0; c < y; c++)
                    serializedValues.Add(array2D[r, c]);
        }

        public void OnAfterDeserialize()
        {
            array2D = new T[x, y];

            int index = 0;

            for (int r = 0; r < x; r++)
                for (int c = 0; c < y; c++)
                {
                    if (index < serializedValues.Count)
                        array2D[r, c] = serializedValues[index];
                    index++;
                }
        }

        public T[,] ToArray()
        {
            return array2D;
        }

        public int GetLength(int dimension)
        {
            return array2D.GetLength(dimension);
        }

        // Immediately set the slot to default(T) without destroying anything.
        public void RemoveAt(int px, int py)
        {
            array2D[px, py] = default;
        }

        // Immediately clear the slot, then schedule the Unity object for destruction at end-of-frame (safe in runtime).
        public void RemoveAndDestroyAt(int px, int py)
        {
            if (array2D[px, py] is UnityEngine.Object u && u != null)
            {
                array2D[px, py] = default;
                UnityEngine.Object.Destroy(u);
            }
            else
            {
                // Non-Unity types or already-null → just clear
                array2D[px, py] = default;
            }
        }

        // Editor-only or special cases: force immediate destruction (use cautiously in play mode).
        public void RemoveAndDestroyImmediateAt(int px, int py)
        {
            if (array2D[px, py] is UnityEngine.Object u && u != null)
            {
                array2D[px, py] = default;
                UnityEngine.Object.DestroyImmediate(u);
            }
            else
            {
                array2D[px, py] = default;
            }
        }

        // True if the slot holds a UnityEngine.Object that is effectively null (destroyed or not assigned)
        public bool IsUnityNullAt(int px, int py)
        {
            var v = array2D[px, py] as UnityEngine.Object;
            return v == null;
        }

        // Sweep grid and clear any entries that are Unity-destroyed (fake-null). Returns number of cleared cells.
        public int PruneDestroyed()
        {
            int removed = 0;
            for (int ix = 0; ix < x; ix++)
            {
                for (int iy = 0; iy < y; iy++)
                {
                    if (array2D[ix, iy] is UnityEngine.Object u && u == null)
                    {
                        array2D[ix, iy] = default;
                        removed++;
                    }
                }
            }
            return removed;
        }
    }


}
