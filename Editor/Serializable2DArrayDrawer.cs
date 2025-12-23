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
            int y = property.FindPropertyRelative("y").intValue;

             return EditorGUIUtility.singleLineHeight * 3 +   // label + row/col input + row order
                   HEADER_SIZE +
                   (HEIGHT_SIZE + PADDING) * y +
                 EditorGUIUtility.standardVerticalSpacing + 6f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var yProp = property.FindPropertyRelative("y");
            var xProp = property.FindPropertyRelative("x");
            var orderProp = property.FindPropertyRelative("rowOrder");
            var serializedValues = property.FindPropertyRelative("serializedValues");

            // Label
            EditorGUI.LabelField(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                label
            );

            float cursorY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Rows & Cols
            float half = (position.width - 20f) * 0.5f;
            var yRect = new Rect(position.x + half + 10f, cursorY, half, EditorGUIUtility.singleLineHeight);
            var xRect = new Rect(position.x, cursorY, half, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            int newX = EditorGUI.IntField(xRect, "X", xProp.intValue);
            int newY = EditorGUI.IntField(yRect, "Y", yProp.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                yProp.intValue = Mathf.Max(1, newY);
                xProp.intValue = Mathf.Max(1, newX);
                property.serializedObject.ApplyModifiedProperties();

                object owner = property.serializedObject.targetObject;
                if (owner != null)
                {
                    // fieldInfo ở đây là field chứa Serializable2DArray<>, nên GetValue trả đúng instance
                    var arrayInstance = fieldInfo.GetValue(owner);
                    if (arrayInstance != null)
                    {
                        dynamic dyn = arrayInstance;
                        dyn.SetSize(xProp.intValue, yProp.intValue);
                    }
                }
                property.serializedObject.Update();
            }

            cursorY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Row order dropdown
            var orderRect = new Rect(position.x, cursorY, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            var newOrder = (BattleTurn.Serializable.Runtime.RowOrder)
                EditorGUI.EnumPopup(orderRect, "Row Order", (BattleTurn.Serializable.Runtime.RowOrder)orderProp.enumValueIndex);
            if (EditorGUI.EndChangeCheck())
            {
                orderProp.enumValueIndex = (int)newOrder;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }

            cursorY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            float width = ComputeCellSize(position.width, xProp.intValue);

            // Headers
            float startX = position.x + HEADER_SIZE;
            float startY = cursorY;
            for (int col = 0; col < xProp.intValue; col++)
            {
                float x = startX + col * (width + PADDING);
                var headerRect = new Rect(x, startY, width, HEADER_SIZE);
                EditorGUI.LabelField(headerRect, col.ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            cursorY += HEADER_SIZE;

            // Cells
            for (int row = 0; row < yProp.intValue; row++)
            {
                float y = cursorY + row * (HEIGHT_SIZE + PADDING);
                // Row header (display label depends on order)
                int displayRow = ((BattleTurn.Serializable.Runtime.RowOrder)orderProp.enumValueIndex) == BattleTurn.Serializable.Runtime.RowOrder.TopToBottom
                    ? row
                    : (yProp.intValue - 1 - row);
                int storageRow = displayRow; // storage indexing uses row index directly
                var rowHeader = new Rect(position.x, y, HEADER_SIZE, HEIGHT_SIZE);
                EditorGUI.LabelField(rowHeader, displayRow.ToString(), EditorStyles.centeredGreyMiniLabel);

                for (int col = 0; col < xProp.intValue; col++)
                {
                    float x = startX + col * (width + PADDING);
                    var cellRect = new Rect(x, y, width, HEIGHT_SIZE);

                    // Flat index matches runtime serialization: r (col) outer, c (row) inner → r*y + c
                    int flat = col * yProp.intValue + storageRow;
                    var cell = serializedValues.GetArrayElementAtIndex(flat);
                    EditorGUI.PropertyField(cellRect, cell, GUIContent.none);
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