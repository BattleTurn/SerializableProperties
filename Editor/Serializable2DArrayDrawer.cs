using UnityEngine;
using UnityEditor;
using BattleTurn.Serializable.Runtime;

namespace SerializableProperties.Editor
{
    [CustomPropertyDrawer(typeof(Serializable2DArray<>), true)]
    public class Serializable2DArrayDrawer : PropertyDrawer
    {
        private const int CELL_SIZE = 25;
        private const int PADDING = 2;
        private const int HEADER_SIZE = 20;
        private const float MAX_WIDTH = 600f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int rows = property.FindPropertyRelative("rows").intValue;
            return EditorGUIUtility.singleLineHeight * 2 +        // label + row/col input
                   HEADER_SIZE +
                   (CELL_SIZE + PADDING) * rows +
                   10;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty rowsProp = property.FindPropertyRelative("rows");
            SerializedProperty colsProp = property.FindPropertyRelative("cols");
            SerializedProperty serializedValues = property.FindPropertyRelative("serializedValues");

            // Draw label
            EditorGUI.LabelField(new Rect(position.x, position.y, MAX_WIDTH, EditorGUIUtility.singleLineHeight), label);

            float cursorY = position.y + EditorGUIUtility.singleLineHeight + 2;

            // --------------------------
            // Rows & Cols input fields
            // --------------------------
            float half = (position.width - 20) * 0.5f;

            Rect rowRect = new Rect(position.x, cursorY, half, EditorGUIUtility.singleLineHeight);
            Rect colRect = new Rect(position.x + half + 10, cursorY, half, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();

            int newRows = EditorGUI.IntField(rowRect, "Rows", rowsProp.intValue);
            int newCols = EditorGUI.IntField(colRect, "Cols", colsProp.intValue);

            if (EditorGUI.EndChangeCheck())
            {
                rowsProp.intValue = Mathf.Max(1, newRows);
                colsProp.intValue = Mathf.Max(1, newCols);

                // Apply field changes BEFORE resizing
                property.serializedObject.ApplyModifiedProperties();

                // Get actual 2d array instance
                object target = fieldInfo.GetValue(property.serializedObject.targetObject);

                if (target != null)
                {
                    var array = (dynamic)target;
                    array.SetSize(rowsProp.intValue, colsProp.intValue);
                }

                property.serializedObject.Update();
            }

            cursorY += EditorGUIUtility.singleLineHeight + 4;

            // ====================
            // Draw Column Headers
            // ====================
            float startX = position.x + HEADER_SIZE;
            float startY = cursorY;

            for (int col = 0; col < colsProp.intValue; col++)
            {
                float x = startX + col * (CELL_SIZE + PADDING);
                Rect headerRect = new Rect(x, startY, CELL_SIZE, HEADER_SIZE);
                EditorGUI.LabelField(headerRect, col.ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            cursorY += HEADER_SIZE;

            // ===== Draw Cells =====

            int index = 0;

            for (int row = 0; row < rowsProp.intValue; row++)
            {
                float y = cursorY + row * (CELL_SIZE + PADDING);

                // Draw row header
                Rect rowHeader = new Rect(position.x, y, HEADER_SIZE, CELL_SIZE);
                EditorGUI.LabelField(rowHeader, row.ToString(), EditorStyles.centeredGreyMiniLabel);

                for (int col = 0; col < colsProp.intValue; col++)
                {
                    float x = startX + col * (CELL_SIZE + PADDING);
                    Rect cellRect = new Rect(x, y, CELL_SIZE, CELL_SIZE);

                    SerializedProperty cell = serializedValues.GetArrayElementAtIndex(index);
                    EditorGUI.PropertyField(cellRect, cell, GUIContent.none);

                    index++;
                }
            }
        }
    }
}