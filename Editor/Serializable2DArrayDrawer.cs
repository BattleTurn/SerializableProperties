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

            // Scrollable grid area with sticky column headers
            int visibleRows = Mathf.Min(yProp.intValue, 5);
            float viewportHeight = HEADER_SIZE + (HEIGHT_SIZE + PADDING) * visibleRows;

            // Sticky header strips (top for columns, left for rows)
            var headerStripRect = new Rect(position.x + HEADER_SIZE, cursorY, position.width - HEADER_SIZE, HEADER_SIZE);
            var rowHeaderStripRect = new Rect(position.x, cursorY + HEADER_SIZE, HEADER_SIZE, viewportHeight - HEADER_SIZE);

            // Scroll view below header strip and right of row headers
            var scrollRect = new Rect(position.x + HEADER_SIZE, cursorY + HEADER_SIZE, position.width - HEADER_SIZE, viewportHeight - HEADER_SIZE);

            float contentWidth = xProp.intValue * (width + PADDING);
            float contentHeight = yProp.intValue * (HEIGHT_SIZE + PADDING);

            // Ensure backing serialized list has enough elements to index into (x * y)
            int required = Mathf.Max(0, xProp.intValue * yProp.intValue);
            if (serializedValues.arraySize != required)
            {
                serializedValues.arraySize = required;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }

            // Draw sticky column headers (aligned with horizontal scroll)
            GUI.BeginGroup(headerStripRect);
            {
                float headerStartX = 0f; // inside header group, columns begin at 0
                float xOffset = -_scroll.x; // follow horizontal scroll
                for (int col = 0; col < xProp.intValue; col++)
                {
                    float x = xOffset + headerStartX + col * (width + PADDING);
                    var hdr = new Rect(x, 0f, width, HEADER_SIZE);
                    if (hdr.xMax >= 0 && hdr.x <= headerStripRect.width)
                        EditorGUI.LabelField(hdr, col.ToString(), EditorStyles.centeredGreyMiniLabel);
                }
            }
            GUI.EndGroup();

            // Sticky row headers (aligned with vertical scroll)
            GUI.BeginGroup(rowHeaderStripRect);
            {
                float yOffset = -_scroll.y;
                for (int row = 0; row < yProp.intValue; row++)
                {
                    float yPos = yOffset + row * (HEIGHT_SIZE + PADDING);
                    if (yPos + HEIGHT_SIZE < 0 || yPos > rowHeaderStripRect.height) continue;
                    int displayRow = ((BattleTurn.Serializable.Runtime.RowOrder)orderProp.enumValueIndex) == BattleTurn.Serializable.Runtime.RowOrder.TopToBottom
                        ? row
                        : (yProp.intValue - 1 - row);
                    var rHdr = new Rect(0f, yPos, HEADER_SIZE, HEIGHT_SIZE);
                    EditorGUI.LabelField(rHdr, displayRow.ToString(), EditorStyles.centeredGreyMiniLabel);
                }
            }
            GUI.EndGroup();

            _scroll = GUI.BeginScrollView(scrollRect, _scroll, new Rect(0, 0, contentWidth, contentHeight));

            // Cells inside scroll view
            float cellStartX = 0f;
            for (int row = 0; row < yProp.intValue; row++)
            {
                float yCell = row * (HEIGHT_SIZE + PADDING);
                // Row header (display label depends on order)
                int displayRow = ((BattleTurn.Serializable.Runtime.RowOrder)orderProp.enumValueIndex) == BattleTurn.Serializable.Runtime.RowOrder.TopToBottom
                    ? row
                    : (yProp.intValue - 1 - row);
                int storageRow = displayRow; // storage indexing uses row index directly

                for (int col = 0; col < xProp.intValue; col++)
                {
                    float xPos = cellStartX + col * (width + PADDING);
                    var cellRect = new Rect(xPos, yCell, width, HEIGHT_SIZE);

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