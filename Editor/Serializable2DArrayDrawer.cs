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
        private Vector2 _scroll = Vector2.zero;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int y = property.FindPropertyRelative("y").intValue;

            int visibleRows = Mathf.Min(y, 5);
            return EditorGUIUtility.singleLineHeight * 3 +   // label + row/col input + row order
                (HEADER_SIZE + (HEIGHT_SIZE + PADDING) * visibleRows) +
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
            var newOrder = (RowOrder)
                EditorGUI.EnumPopup(orderRect, "Row Order", (RowOrder)orderProp.enumValueIndex);
            if (EditorGUI.EndChangeCheck())
            {
                orderProp.enumValueIndex = (int)newOrder;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }

            cursorY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            float width = ComputeCellSize(position.width, xProp.intValue);

            // Scrollable grid area
            int visibleRows = Mathf.Min(yProp.intValue, 5);
            float viewportHeight = HEADER_SIZE + (HEIGHT_SIZE + PADDING) * visibleRows;
            var scrollRect = new Rect(position.x, cursorY, position.width, viewportHeight);

            float contentWidth = HEADER_SIZE + xProp.intValue * (width + PADDING);
            float contentHeight = HEADER_SIZE + yProp.intValue * (HEIGHT_SIZE + PADDING);
            bool showH = contentWidth > scrollRect.width;
            bool showV = yProp.intValue > visibleRows;

            _scroll = GUI.BeginScrollView(scrollRect, _scroll, new Rect(0, 0, contentWidth, contentHeight), showH, showV);

            // Headers inside scroll view (local origin)
            float startX = HEADER_SIZE;
            float startY = 0f;
            for (int col = 0; col < xProp.intValue; col++)
            {
                float x = startX + col * (width + PADDING);
                var headerRect = new Rect(x, startY, width, HEADER_SIZE);
                EditorGUI.LabelField(headerRect, col.ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            // Cells inside scroll view
            for (int row = 0; row < yProp.intValue; row++)
            {
                float yCell = HEADER_SIZE + row * (HEIGHT_SIZE + PADDING);
                // Row header (display label depends on order)
                int displayRow = ((BattleTurn.Serializable.Runtime.RowOrder)orderProp.enumValueIndex) == BattleTurn.Serializable.Runtime.RowOrder.TopToBottom
                    ? row
                    : (yProp.intValue - 1 - row);
                int storageRow = displayRow; // storage indexing uses row index directly
                var rowHeader = new Rect(0f, yCell, HEADER_SIZE, HEIGHT_SIZE);
                EditorGUI.LabelField(rowHeader, displayRow.ToString(), EditorStyles.centeredGreyMiniLabel);

                for (int col = 0; col < xProp.intValue; col++)
                {
                    float x = startX + col * (width + PADDING);
                    var cellRect = new Rect(x, yCell, width, HEIGHT_SIZE);

                    // Flat index matches runtime serialization: r (col) outer, c (row) inner → r*y + c
                    int flat = col * yProp.intValue + storageRow;
                    var cell = serializedValues.GetArrayElementAtIndex(flat);
                    EditorGUI.PropertyField(cellRect, cell, GUIContent.none);
                }
            }

            GUI.EndScrollView();
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