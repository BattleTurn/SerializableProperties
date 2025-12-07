using UnityEngine;
using UnityEditor;
using BattleTurn.Serializable.Runtime;

namespace SerializableProperties.Editor
{
    [CustomPropertyDrawer(typeof(Serializable2DArray<>), true)]
    public class Serializable2DArrayDrawer : PropertyDrawer
    {
        private const int HEIGHT_SIZE = 20;
        private const int PADDING = 2;
        private const int HEADER_SIZE = 20;
        private const int MIN_WIDTH_SIZE = 30;
        private const int MAX_WIDTH_SIZE = 120;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int rows = property.FindPropertyRelative("rows").intValue;
            int cols = property.FindPropertyRelative("cols").intValue;

            return EditorGUIUtility.singleLineHeight * 2 +   // label + row/col input
                   HEADER_SIZE +
                   (HEIGHT_SIZE + PADDING) * rows +
                   EditorGUIUtility.standardVerticalSpacing + 6f;;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var rowsProp = property.FindPropertyRelative("rows");
            var colsProp = property.FindPropertyRelative("cols");
            var serializedValues = property.FindPropertyRelative("serializedValues");

            // Label
            EditorGUI.LabelField(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                label
            );

            float cursorY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Rows & Cols
            float half = (position.width - 20f) * 0.5f;
            var rowRect = new Rect(position.x, cursorY, half, EditorGUIUtility.singleLineHeight);
            var colRect = new Rect(position.x + half + 10f, cursorY, half, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            int newRows = EditorGUI.IntField(rowRect, "Rows", rowsProp.intValue);
            int newCols = EditorGUI.IntField(colRect, "Cols", colsProp.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                rowsProp.intValue = Mathf.Max(1, newRows);
                colsProp.intValue = Mathf.Max(1, newCols);
                property.serializedObject.ApplyModifiedProperties();

                object owner = property.serializedObject.targetObject;
                if (owner != null)
                {
                    // fieldInfo ở đây là field chứa Serializable2DArray<>, nên GetValue trả đúng instance
                    var arrayInstance = fieldInfo.GetValue(owner);
                    if (arrayInstance != null)
                    {
                        dynamic dyn = arrayInstance;
                        dyn.SetSize(rowsProp.intValue, colsProp.intValue);
                    }
                }
                property.serializedObject.Update();
            }

            cursorY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            float width = ComputeCellSize(position.width, colsProp.intValue);

            // Headers
            float startX = position.x + HEADER_SIZE;
            float startY = cursorY;
            for (int col = 0; col < colsProp.intValue; col++)
            {
                float x = startX + col * (width + PADDING);
                var headerRect = new Rect(x, startY, width, HEADER_SIZE);
                EditorGUI.LabelField(headerRect, col.ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            cursorY += HEADER_SIZE;

            // Cells
            int index = 0;
            for (int row = 0; row < rowsProp.intValue; row++)
            {
                float y = cursorY + row * (HEIGHT_SIZE + PADDING);
                // Row header
                var rowHeader = new Rect(position.x, y, HEADER_SIZE, HEIGHT_SIZE);
                EditorGUI.LabelField(rowHeader, row.ToString(), EditorStyles.centeredGreyMiniLabel);

                for (int col = 0; col < colsProp.intValue; col++)
                {
                    float x = startX + col * (width + PADDING);
                    var cellRect = new Rect(x, y, width, HEIGHT_SIZE);

                    var cell = serializedValues.GetArrayElementAtIndex(index);
                    EditorGUI.PropertyField(cellRect, cell, GUIContent.none);

                    index++;
                }
            }
        }

        private static float ComputeCellSize(float totalWidth, int cols)
        {
            if (cols <= 0) cols = 1;
            float available = Mathf.Max(0f, totalWidth - HEADER_SIZE - 8f);
            float cell = (available - (cols - 1) * PADDING) / cols;
            return Mathf.Clamp(cell, MIN_WIDTH_SIZE, MAX_WIDTH_SIZE);
        }
    }
}