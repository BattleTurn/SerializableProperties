using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    [Serializable]
    public class Serializable2DArray<T> : ISerializationCallbackReceiver
    {
        [SerializeField] private int rows;
        [SerializeField] private int cols;

        [SerializeField] private List<T> serializedValues = new();

        [NonSerialized] private T[,] array2D;

        public int Rows => rows;
        public int Cols => cols;

        public void SetSize(int newRows, int newCols)
        {
            if (newRows == rows && newCols == cols)
                return;

            rows = Mathf.Max(1, newRows);
            cols = Mathf.Max(1, newCols);

            // Resize the 2D array
            T[,] newArray = new T[rows, cols];

            if (array2D != null)
            {
                int minR = Mathf.Min(rows, array2D.GetLength(0));
                int minC = Mathf.Min(cols, array2D.GetLength(1));

                // copy old data → new data
                for (int r = 0; r < minR; r++)
                    for (int c = 0; c < minC; c++)
                        newArray[r, c] = array2D[r, c];
            }

            array2D = newArray;
            serializedValues = new List<T>(rows * cols);
        }

        public T this[int r, int c]
        {
            get => array2D[r, c];
            set => array2D[r, c] = value;
        }

        public void OnBeforeSerialize()
        {
            serializedValues.Clear();
            if (array2D == null)
                array2D = new T[rows, cols];

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    serializedValues.Add(array2D[r, c]);
        }

        public void OnAfterDeserialize()
        {
            array2D = new T[rows, cols];

            int index = 0;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (index < serializedValues.Count)
                        array2D[r, c] = serializedValues[index];
                    index++;
                }
        }
    }

}