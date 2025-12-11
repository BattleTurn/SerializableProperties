using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    [Serializable]
    public class Serializable2DArray<T> : ISerializationCallbackReceiver
    {
        [SerializeField] private int y;
        [SerializeField] private int x;

        [SerializeField] private List<T> serializedValues = new();

        [NonSerialized] private T[,] array2D;

        public int Y => y;
        public int X => x;

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
    }

}