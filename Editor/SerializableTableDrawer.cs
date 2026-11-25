using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Editor
{
    /// <summary>
    /// Draws a SerializableTable as a spreadsheet: row numbers, a sticky header of column
    /// names, grid lines, keyboard movement between cells and TSV copy/paste.
    /// IMGUI rather than UI Toolkit because the default Inspector still renders through
    /// IMGUI, and a drawer with only CreatePropertyGUI shows "No GUI Implemented" there.
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableTable<>), true)]
    public class SerializableTableDrawer : PropertyDrawer
    {
        private const float ROW_HEIGHT = 20f;
        private const float HEADER_HEIGHT = 20f;
        private const float HANDLE_WIDTH = 16f;
        private const float NUMBER_WIDTH = 28f;
        private const float GUTTER_WIDTH = HANDLE_WIDTH + NUMBER_WIDTH;
        private const float MIN_COLUMN_WIDTH = 70f;
        private const float CELL_INSET = 3f;
        private const int MAX_VISIBLE_ROWS = 12;

        private static Color GridColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.10f)
            : new Color(0f, 0f, 0f, 0.18f);

        private static Color HeaderBackground => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.07f)
            : new Color(0f, 0f, 0f, 0.07f);

        private static Color AlternateRowColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.025f)
            : new Color(0f, 0f, 0f, 0.025f);

        private static Color SelectionColor => new Color(0.3f, 0.55f, 0.95f, 0.22f);

        private static Color HandleColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.35f)
            : new Color(0f, 0f, 0f, 0.4f);

        private static Color HandleActiveColor => new Color(0.4f, 0.7f, 1f, 0.95f);

        // The hole the dragged row will drop into.
        private static Color GapColor => EditorGUIUtility.isProSkin
            ? new Color(0f, 0f, 0f, 0.3f)
            : new Color(0f, 0f, 0f, 0.12f);

        private static Color FloatingRowColor => EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.29f, 0.37f, 0.98f)
            : new Color(0.82f, 0.86f, 0.93f, 0.98f);

        private static void VLine(float x, float y, float height) =>
            EditorGUI.DrawRect(new Rect(x, y, 1f, height), GridColor);

        private static void HLine(float x, float y, float width) =>
            EditorGUI.DrawRect(new Rect(x, y, width, 1f), GridColor);

        private static void DrawBorder(Rect rect)
        {
            HLine(rect.x, rect.y, rect.width);
            HLine(rect.x, rect.yMax - 1f, rect.width);
            VLine(rect.x, rect.y, rect.height);
            VLine(rect.xMax - 1f, rect.y, rect.height);
        }

        /// <summary>
        /// Per-field view state. Static because Unity reuses one drawer instance across
        /// every property it draws, so an instance field would leak between tables.
        /// </summary>
        private sealed class TableState
        {
            public Vector2 Scroll;
            public readonly HashSet<int> Selected = new();
            public int Anchor = -1;
            public int DragFrom = -1;
            public int DragTo = -1;
            public float DragMouseY;
            public string PendingFocus;
        }

        private static readonly Dictionary<string, TableState> States = new();

        private static TableState GetState(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            string key = (target == null ? 0 : target.GetInstanceID()) + ":" + property.propertyPath;

            if (!States.TryGetValue(key, out TableState state))
            {
                state = new TableState();
                States[key] = state;
            }

            return state;
        }

        private static List<string> ColumnPaths(Type elementType, out List<string> headers)
        {
            var paths = new List<string>();
            headers = new List<string>();

            foreach (FieldInfo f in SerializedFieldScanner.GetColumns(elementType))
            {
                paths.Add(f.Name);
                headers.Add(ObjectNames.NicifyVariableName(f.Name));
            }

            // Primitives, strings, enums and Object references have nothing to spread
            // across columns, so the whole element is the one cell.
            if (paths.Count == 0)
            {
                paths.Add(null);
                headers.Add("Value");
            }

            return paths;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty rows = property.FindPropertyRelative("rows");
            if (rows == null || !rows.isArray)
                return EditorGUIUtility.singleLineHeight;

            int visible = Mathf.Clamp(rows.arraySize, 1, MAX_VISIBLE_ROWS);

            return EditorGUIUtility.singleLineHeight          // toolbar
                 + HEADER_HEIGHT
                 + visible * ROW_HEIGHT
                 + EditorGUIUtility.singleLineHeight          // footer
                 + EditorGUIUtility.standardVerticalSpacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty rows = property.FindPropertyRelative("rows");
            if (rows == null || !rows.isArray)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Unsupported table field"));
                return;
            }

            TableState state = GetState(property);

            // One id per OnGUI call, in a fixed place, so Layout and Repaint agree on it.
            int dragId = GUIUtility.GetControlID(FocusType.Passive);

            List<string> paths = ColumnPaths(GetElementType(fieldInfo?.FieldType), out List<string> headers);

            int visible = Mathf.Clamp(rows.arraySize, 1, MAX_VISIBLE_ROWS);
            float viewportHeight = visible * ROW_HEIGHT;
            float viewportWidth = position.width - GUTTER_WIDTH;
            bool needsVerticalScroll = rows.arraySize > MAX_VISIBLE_ROWS;

            // Keys first, so a move that scrolls lands before anything is laid out.
            HandleKeys(state, rows.arraySize, paths.Count, viewportHeight);

            float cursorY = position.y;

            var toolbar = new Rect(position.x, cursorY, position.width, EditorGUIUtility.singleLineHeight);
            DrawToolbar(toolbar, property, rows, state, paths, headers, label);
            cursorY += toolbar.height + EditorGUIUtility.standardVerticalSpacing;

            // Leave room for the scrollbar so the last column is never half hidden.
            float usableWidth = viewportWidth - (needsVerticalScroll ? 15f : 2f);
            float columnWidth = Mathf.Max(MIN_COLUMN_WIDTH, usableWidth / paths.Count);
            float contentWidth = paths.Count * columnWidth;
            float contentHeight = rows.arraySize * ROW_HEIGHT;

            var gridRect = new Rect(position.x, cursorY, position.width, HEADER_HEIGHT + viewportHeight);

            var headerRect = new Rect(position.x, cursorY, position.width, HEADER_HEIGHT);
            DrawHeader(headerRect, headers, columnWidth, state.Scroll.x);
            cursorY += HEADER_HEIGHT;

            var bodyRect = new Rect(position.x, cursorY, position.width, viewportHeight);
            var gutterRect = new Rect(position.x, cursorY, GUTTER_WIDTH, viewportHeight);
            var scrollRect = new Rect(position.x + GUTTER_WIDTH, cursorY, viewportWidth, viewportHeight);

            HandleRowDrag(gutterRect, rows, state, dragId);
            DrawGutter(gutterRect, rows.arraySize, state);

            state.Scroll = GUI.BeginScrollView(
                scrollRect, state.Scroll, new Rect(0f, 0f, contentWidth, contentHeight));

            // Only the rows inside the viewport get drawn: every drawn cell is a real IMGUI
            // control, so a thousand row table would otherwise build thousands of them on
            // every single event.
            FirstAndLastVisible(state.Scroll.y, viewportHeight, rows.arraySize, out int first, out int last);

            for (int slot = first; slot <= last; slot++)
            {
                float y = slot * ROW_HEIGHT;
                var rowRect = new Rect(0f, y, contentWidth, ROW_HEIGHT);

                // While dragging, the slot under the pointer is left empty and every other
                // slot shows the row that would sit there once the drop happens.
                if (slot == state.DragTo && state.DragFrom >= 0)
                {
                    EditorGUI.DrawRect(rowRect, GapColor);
                    HLine(0f, y + ROW_HEIGHT - 1f, contentWidth);
                    continue;
                }

                int index = DataIndexForSlot(slot, state.DragFrom, state.DragTo);
                if (index < 0 || index >= rows.arraySize)
                    continue;

                if (slot % 2 == 1)
                    EditorGUI.DrawRect(rowRect, AlternateRowColor);
                if (state.DragFrom < 0 && state.Selected.Contains(index))
                    EditorGUI.DrawRect(rowRect, SelectionColor);

                SerializedProperty element = rows.GetArrayElementAtIndex(index);

                for (int col = 0; col < paths.Count; col++)
                {
                    var cellRect = new Rect(
                        col * columnWidth + CELL_INSET,
                        y + 1f,
                        columnWidth - CELL_INSET * 2f,
                        ROW_HEIGHT - 3f);

                    SerializedProperty cell = paths[col] == null
                        ? element
                        : element.FindPropertyRelative(paths[col]);

                    if (cell == null)
                    {
                        EditorGUI.LabelField(cellRect, "-");
                        continue;
                    }

                    GUI.SetNextControlName(CellName(index, col));
                    EditorGUI.PropertyField(cellRect, cell, GUIContent.none);
                }

                HLine(0f, y + ROW_HEIGHT - 1f, contentWidth);
            }

            // One pass for the column rules so they run the whole height, not per row.
            for (int col = 1; col < paths.Count; col++)
                VLine(col * columnWidth, 0f, contentHeight);

            GUI.EndScrollView();
            cursorY += viewportHeight;

            DrawBorder(gridRect);
            DrawFloatingRow(bodyRect, scrollRect, rows, state, paths, columnWidth);

            var footer = new Rect(position.x, cursorY, position.width, EditorGUIUtility.singleLineHeight);
            DrawFooter(footer, rows, state);

            if (state.PendingFocus != null)
            {
                GUI.FocusControl(state.PendingFocus);
                state.PendingFocus = null;
                EditorWindow.focusedWindow?.Repaint();
            }
        }

        /// <summary>
        /// Which row belongs in a slot while a drag is in flight: the list with the dragged
        /// row taken out and put back at the drop point. Without a drag it is the identity.
        /// </summary>
        public static int DataIndexForSlot(int slot, int from, int to)
        {
            if (from < 0 || to < 0)
                return slot;

            if (slot == to)
                return from;

            // Index into the sequence that has the dragged row removed.
            int without = slot < to ? slot : slot - 1;
            return without < from ? without : without + 1;
        }

        /// <summary>Inclusive row range covered by the viewport at the given scroll offset.</summary>
        public static void FirstAndLastVisible(
            float scrollY, float viewportHeight, int rowCount, out int first, out int last)
        {
            if (rowCount <= 0)
            {
                first = 0;
                last = -1;
                return;
            }

            first = Mathf.Clamp(Mathf.FloorToInt(scrollY / ROW_HEIGHT), 0, rowCount - 1);
            last = Mathf.Clamp(Mathf.CeilToInt((scrollY + viewportHeight) / ROW_HEIGHT), first, rowCount - 1);
        }

        private static void DrawToolbar(
            Rect rect,
            SerializedProperty property,
            SerializedProperty rows,
            TableState state,
            List<string> paths,
            List<string> headers,
            GUIContent label)
        {
            const float BUTTON_WIDTH = 74f;

            var titleRect = new Rect(rect.x, rect.y, rect.width - BUTTON_WIDTH * 2f - 4f, rect.height);
            EditorGUI.LabelField(titleRect, label.text + "  (" + rows.arraySize + ")", EditorStyles.boldLabel);

            var copyRect = new Rect(rect.xMax - BUTTON_WIDTH * 2f - 2f, rect.y, BUTTON_WIDTH, rect.height);
            var pasteRect = new Rect(rect.xMax - BUTTON_WIDTH, rect.y, BUTTON_WIDTH, rect.height);

            if (GUI.Button(copyRect, "Copy TSV", EditorStyles.miniButtonLeft))
                Copy(rows, state, paths, headers);

            if (GUI.Button(pasteRect, "Paste TSV", EditorStyles.miniButtonRight))
                Paste(property, rows, state, paths, headers);
        }

        private static void DrawHeader(Rect rect, List<string> headers, float columnWidth, float scrollX)
        {
            EditorGUI.DrawRect(rect, HeaderBackground);

            var numberHeader = new Rect(rect.x + HANDLE_WIDTH, rect.y, NUMBER_WIDTH, rect.height);
            EditorGUI.LabelField(numberHeader, "#", EditorStyles.centeredGreyMiniLabel);

            var strip = new Rect(rect.x + GUTTER_WIDTH, rect.y, rect.width - GUTTER_WIDTH, rect.height);
            GUI.BeginGroup(strip);

            for (int col = 0; col < headers.Count; col++)
            {
                float x = -scrollX + col * columnWidth;

                // Skip what the horizontal scroll has pushed out of sight.
                if (x + columnWidth < 0f || x > strip.width)
                    continue;

                var cell = new Rect(x + CELL_INSET, 0f, columnWidth - CELL_INSET * 2f, rect.height);
                EditorGUI.LabelField(cell, headers[col], EditorStyles.miniBoldLabel);

                if (col > 0)
                    VLine(x, 0f, rect.height);
            }

            GUI.EndGroup();

            // The gutter rule and the line under the whole header.
            VLine(rect.x + GUTTER_WIDTH, rect.y, rect.height);
            HLine(rect.x, rect.yMax - 1f, rect.width);
        }

        /// <summary>The drag handle and row number column down the left edge.</summary>
        private static void DrawGutter(Rect rect, int rowCount, TableState state)
        {
            GUI.BeginGroup(rect);

            for (int slot = 0; slot < rowCount; slot++)
            {
                float y = -state.Scroll.y + slot * ROW_HEIGHT;
                if (y + ROW_HEIGHT < 0f || y > rect.height)
                    continue;

                var cell = new Rect(0f, y, GUTTER_WIDTH, ROW_HEIGHT);

                if (slot == state.DragTo && state.DragFrom >= 0)
                {
                    EditorGUI.DrawRect(cell, GapColor);
                    HLine(0f, y + ROW_HEIGHT - 1f, GUTTER_WIDTH);
                    continue;
                }

                int index = DataIndexForSlot(slot, state.DragFrom, state.DragTo);

                if (slot % 2 == 1)
                    EditorGUI.DrawRect(cell, AlternateRowColor);
                if (state.DragFrom < 0 && state.Selected.Contains(index))
                    EditorGUI.DrawRect(cell, SelectionColor);

                DrawHandle(new Rect(0f, y, HANDLE_WIDTH, ROW_HEIGHT), false);

                // Input lives in HandleRowDrag: a press here may be a click or a drag,
                // and a Button would swallow it before we can tell which.
                var number = new Rect(HANDLE_WIDTH, y, NUMBER_WIDTH, ROW_HEIGHT);
                EditorGUI.LabelField(number, (slot + 1).ToString(), EditorStyles.centeredGreyMiniLabel);

                HLine(0f, y + ROW_HEIGHT - 1f, GUTTER_WIDTH);
            }

            GUI.EndGroup();

            VLine(rect.xMax - 1f, rect.y, rect.height);
        }

        /// <summary>The three bars that say a row can be dragged, same idea as a list handle.</summary>
        private static void DrawHandle(Rect rect, bool active)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            const float BAR_WIDTH = 8f;
            const float BAR_GAP = 3f;

            Color color = active ? HandleActiveColor : HandleColor;
            float x = rect.x + (rect.width - BAR_WIDTH) * 0.5f;
            float y = rect.y + rect.height * 0.5f - BAR_GAP - 1f;

            for (int i = 0; i < 3; i++)
                EditorGUI.DrawRect(new Rect(x, y + i * BAR_GAP, BAR_WIDTH, 1f), color);
        }

        /// <summary>
        /// Drag the handle to move a row, the way a list handle works. Also carries the
        /// plain click that selects a row, because a press cannot be told apart from the
        /// start of a drag until the mouse comes back up.
        /// </summary>
        private static void HandleRowDrag(Rect gutter, SerializedProperty rows, TableState state, int id)
        {
            Event e = Event.current;
            if (e == null || rows.arraySize == 0)
                return;


            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !gutter.Contains(e.mousePosition))
                        break;

                    int pressed = RowAtPoint(gutter, e.mousePosition, state, rows.arraySize, false);
                    if (pressed < 0)
                        break;

                    // Shift and ctrl are selection gestures. Starting a reorder from them
                    // would reorder the table while the user is only picking rows, so the
                    // selection is applied here and no drag begins.
                    if (e.shift || e.control || e.command)
                    {
                        state.Anchor = ApplySelectionClick(
                            state.Selected, state.Anchor, pressed, rows.arraySize,
                            e.shift, e.control || e.command);
                        e.Use();
                        break;
                    }

                    state.DragFrom = pressed;
                    state.DragTo = pressed;
                    state.DragMouseY = e.mousePosition.y;
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id)
                        break;

                    // Nudge the view when the pointer leaves the top or bottom edge, or a
                    // long table could never be reordered beyond one screenful.
                    if (e.mousePosition.y < gutter.y)
                        state.Scroll.y = Mathf.Max(0f, state.Scroll.y - ROW_HEIGHT * 0.5f);
                    else if (e.mousePosition.y > gutter.yMax)
                        state.Scroll.y = Mathf.Min(
                            Mathf.Max(0f, rows.arraySize * ROW_HEIGHT - gutter.height),
                            state.Scroll.y + ROW_HEIGHT * 0.5f);

                    state.DragMouseY = e.mousePosition.y;
                    state.DragTo = RowAtPoint(gutter, e.mousePosition, state, rows.arraySize, true);
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id)
                        break;

                    GUIUtility.hotControl = 0;

                    int from = state.DragFrom;
                    int to = state.DragTo;
                    state.DragFrom = -1;
                    state.DragTo = -1;

                    if (from >= 0 && to >= 0 && to != from)
                    {
                        rows.MoveArrayElement(from, to);
                        rows.serializedObject.ApplyModifiedProperties();
                        state.Selected.Clear();
                        state.Selected.Add(to);
                        state.Anchor = to;
                    }
                    else if (from >= 0)
                    {
                        // A press that never moved is a plain click. Modifier clicks never
                        // get here: they are handled on mouse down and start no drag.
                        state.Anchor = ApplySelectionClick(
                            state.Selected, state.Anchor, from, rows.arraySize,
                            shift: false, toggle: false);
                    }

                    e.Use();
                    break;
            }
        }

        /// <summary>
        /// Applies a click on a row number to the selection and returns the new anchor.
        /// Shift extends from the anchor to the clicked row, Ctrl (or Cmd on a Mac) adds and
        /// removes one row at a time, and a plain click keeps only that row - or clears the
        /// selection when that row was already the only one in it.
        /// Takes the plain set rather than the view state so it can be tested on its own.
        /// </summary>
        public static int ApplySelectionClick(
            HashSet<int> selected, int anchor, int row, int rowCount, bool shift, bool toggle)
        {
            if (selected == null || row < 0 || row >= rowCount)
                return anchor;

            // Rows may have been deleted since the anchor was set.
            if (anchor >= rowCount)
                anchor = -1;

            if (shift && anchor >= 0)
            {
                int low = Mathf.Min(anchor, row);
                int high = Mathf.Max(anchor, row);

                selected.Clear();
                for (int i = low; i <= high; i++)
                    selected.Add(i);

                // The anchor stays put so the range can be stretched again from it.
                return anchor;
            }

            if (toggle)
            {
                if (!selected.Remove(row))
                    selected.Add(row);

                return row;
            }

            bool wasTheOnlyOne = selected.Count == 1 && selected.Contains(row);
            selected.Clear();

            if (wasTheOnlyOne)
                return -1;

            selected.Add(row);
            return row;
        }

        /// <summary>Row under a point in the gutter, or -1. Clamping keeps a drag usable past the edge.</summary>
        private static int RowAtPoint(Rect gutter, Vector2 point, TableState state, int rowCount, bool clamp)
        {
            if (rowCount == 0)
                return -1;

            int row = Mathf.FloorToInt((point.y - gutter.y + state.Scroll.y) / ROW_HEIGHT);

            if (clamp)
                return Mathf.Clamp(row, 0, rowCount - 1);

            return row >= 0 && row < rowCount && gutter.Contains(point) ? row : -1;
        }

        /// <summary>
        /// The dragged row, lifted out of the list and following the pointer. Drawn as flat
        /// text rather than live fields: a second set of controls for the same properties
        /// would shift every control id while the drag is in flight.
        /// </summary>
        private static void DrawFloatingRow(
            Rect body,
            Rect cellArea,
            SerializedProperty rows,
            TableState state,
            List<string> paths,
            float columnWidth)
        {
            if (state.DragFrom < 0 || state.DragFrom >= rows.arraySize)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            float y = Mathf.Clamp(
                state.DragMouseY - ROW_HEIGHT * 0.5f, body.y, body.yMax - ROW_HEIGHT);

            var floating = new Rect(body.x, y, body.width, ROW_HEIGHT);
            EditorGUI.DrawRect(floating, FloatingRowColor);
            DrawBorder(floating);

            DrawHandle(new Rect(floating.x, y, HANDLE_WIDTH, ROW_HEIGHT), true);
            EditorGUI.LabelField(
                new Rect(floating.x + HANDLE_WIDTH, y, NUMBER_WIDTH, ROW_HEIGHT),
                (state.DragTo + 1).ToString(),
                EditorStyles.centeredGreyMiniLabel);

            SerializedProperty element = rows.GetArrayElementAtIndex(state.DragFrom);

            GUI.BeginClip(new Rect(cellArea.x, y, cellArea.width, ROW_HEIGHT));

            for (int col = 0; col < paths.Count; col++)
            {
                float x = -state.Scroll.x + col * columnWidth + CELL_INSET;
                if (x + columnWidth < 0f || x > cellArea.width)
                    continue;

                SerializedProperty cell = paths[col] == null
                    ? element
                    : element.FindPropertyRelative(paths[col]);

                // ReadCell already knows how to turn any supported cell into text.
                string text = TableClipboard.ReadCell(cell) ?? "...";
                EditorGUI.LabelField(
                    new Rect(x, 0f, columnWidth - CELL_INSET * 2f, ROW_HEIGHT),
                    text,
                    EditorStyles.miniLabel);
            }

            GUI.EndClip();
        }

        private static void DrawFooter(Rect rect, SerializedProperty rows, TableState state)
        {
            const float BUTTON_WIDTH = 26f;

            var hintRect = new Rect(rect.x, rect.y, rect.width - BUTTON_WIDTH * 2f - 4f, rect.height);
            EditorGUI.LabelField(hintRect, FooterHint(state), EditorStyles.miniLabel);

            var addRect = new Rect(rect.xMax - BUTTON_WIDTH * 2f - 2f, rect.y, BUTTON_WIDTH, rect.height);
            var removeRect = new Rect(rect.xMax - BUTTON_WIDTH, rect.y, BUTTON_WIDTH, rect.height);

            if (GUI.Button(addRect, "+", EditorStyles.miniButtonLeft))
            {
                int at = state.Selected.Count > 0 ? state.Selected.Max() + 1 : rows.arraySize;
                rows.InsertArrayElementAtIndex(Mathf.Clamp(at, 0, rows.arraySize));
                rows.serializedObject.ApplyModifiedProperties();
            }

            using (new EditorGUI.DisabledScope(rows.arraySize == 0))
            {
                if (GUI.Button(removeRect, "-", EditorStyles.miniButtonRight))
                {
                    // Highest index first, or deleting one would shift the rest out from
                    // under the indices still to come.
                    List<int> doomed = state.Selected.Count > 0
                        ? state.Selected.Where(i => i >= 0 && i < rows.arraySize)
                                        .OrderByDescending(i => i)
                                        .ToList()
                        : new List<int> { rows.arraySize - 1 };

                    foreach (int index in doomed)
                        DeleteRow(rows, index);

                    rows.serializedObject.ApplyModifiedProperties();
                    state.Selected.Clear();
                    state.Anchor = -1;
                }
            }
        }

        private static string FooterHint(TableState state)
        {
            if (state.Selected.Count == 0)
                return "Shift-click and ctrl-click select rows. Drag the handle to reorder.";

            return state.Selected.Count == 1
                ? "Row " + (state.Selected.Min() + 1) + " selected"
                : state.Selected.Count + " rows selected";
        }

        /// <summary>
        /// Removing a row of object references takes two deletes: Unity spends the first one
        /// setting the reference to null and only the second one removes the slot.
        /// </summary>
        private static void DeleteRow(SerializedProperty rows, int index)
        {
            SerializedProperty element = rows.GetArrayElementAtIndex(index);

            if (element.propertyType == SerializedPropertyType.ObjectReference
                && element.objectReferenceValue != null)
                element.objectReferenceValue = null;

            rows.DeleteArrayElementAtIndex(index);
        }

        private static void Copy(
            SerializedProperty rows, TableState state, List<string> paths, List<string> headers)
        {
            if (rows.arraySize == 0)
            {
                Debug.LogWarning("Nothing to copy: the table is empty.");
                return;
            }

            var indices = new List<int>();
            if (state.Selected.Count > 0)
                indices.AddRange(state.Selected
                    .Where(i => i >= 0 && i < rows.arraySize)
                    .OrderBy(i => i));
            else
                for (int i = 0; i < rows.arraySize; i++) indices.Add(i);

            GUIUtility.systemCopyBuffer = TableClipboard.ToTsv(rows, indices, paths, headers);
            Debug.Log("Copied " + indices.Count + " row(s) as tab separated text.");
        }

        private static void Paste(
            SerializedProperty property, SerializedProperty rows,
            TableState state, List<string> paths, List<string> headers)
        {
            string tsv = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(tsv))
            {
                Debug.LogWarning("Nothing to paste: the clipboard holds no text.");
                return;
            }

            int start = state.Selected.Count > 0 ? state.Selected.Min() : 0;
            int written = TableClipboard.Paste(rows, start, paths, tsv, headers, out int skipped);

            if (written == 0 && skipped == 0)
            {
                Debug.LogWarning("Nothing was pasted: the clipboard text did not parse as rows.");
                return;
            }

            // ApplyModifiedProperties records its own undo step.
            property.serializedObject.ApplyModifiedProperties();

            if (skipped > 0)
                Debug.LogWarning("Pasted " + written + " cell(s) from row " + (start + 1) + "; "
                    + skipped + " cell(s) were left alone because the text did not fit the field type.");
            else
                Debug.Log("Pasted " + written + " cell(s) from row " + (start + 1) + ".");
        }

        // Tab already walks the controls in draw order, which is row by row - the same as a
        // spreadsheet. Only the vertical moves need handling.
        private static void HandleKeys(TableState state, int rowCount, int columnCount, float viewportHeight)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || rowCount == 0)
                return;

            if (!TryParseCellName(GUI.GetNameOfFocusedControl(), out int row, out int col))
                return;

            int step;
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    step = e.shift ? -1 : 1;
                    break;
                case KeyCode.DownArrow:
                    step = 1;
                    break;
                case KeyCode.UpArrow:
                    step = -1;
                    break;
                default:
                    // Left and right arrows stay with the text caret.
                    return;
            }

            int target = Mathf.Clamp(row + step, 0, rowCount - 1);
            if (target == row || col >= columnCount)
                return;

            // Culled rows have no control to focus, so bring the target into view first.
            EnsureRowVisible(state, target, viewportHeight);
            state.PendingFocus = CellName(target, col);
            e.Use();
        }

        private static void EnsureRowVisible(TableState state, int row, float viewportHeight)
        {
            float top = row * ROW_HEIGHT;
            float bottom = top + ROW_HEIGHT;

            if (top < state.Scroll.y)
                state.Scroll.y = top;
            else if (bottom > state.Scroll.y + viewportHeight)
                state.Scroll.y = bottom - viewportHeight;
        }

        private static string CellName(int row, int column) => "cell_" + row + "_" + column;

        private static bool TryParseCellName(string name, out int row, out int column)
        {
            row = column = -1;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("cell_", StringComparison.Ordinal))
                return false;

            string[] parts = name.Split('_');
            return parts.Length == 3
                && int.TryParse(parts[1], out row)
                && int.TryParse(parts[2], out column);
        }

        /// <summary>Unwrap List, array and subclass declarations down to the T of SerializableTable.</summary>
        public static Type GetElementType(Type fieldType)
        {
            Type t = fieldType;
            if (t == null)
                return null;

            if (t.IsArray)
                t = t.GetElementType();
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                t = t.GetGenericArguments()[0];

            while (t != null && t != typeof(object))
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SerializableTable<>))
                    return t.GetGenericArguments()[0];
                t = t.BaseType;
            }

            return null;
        }
    }
}
