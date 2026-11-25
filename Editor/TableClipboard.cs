using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BattleTurn.Serializable.Editor
{
    /// <summary>
    /// Turns table rows into tab-separated text and back, so rows round-trip through
    /// Excel, Sheets or any text editor. Kept free of UI so it can be tested directly.
    /// </summary>
    public static class TableClipboard
    {
        private const char TAB = '\t';
        private const char LF = '\n';
        private const char CR = '\r';
        private const char DQUOTE = '"';

        /// <summary>Cell text for a property, or null when the type is not supported.</summary>
        public static string ReadCell(SerializedProperty cell)
        {
            if (cell == null) return null;

            switch (cell.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return cell.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return cell.boolValue ? "TRUE" : "FALSE";
                case SerializedPropertyType.Float:
                    return cell.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return cell.stringValue ?? string.Empty;
                case SerializedPropertyType.Enum:
                    return cell.enumValueIndex >= 0 && cell.enumValueIndex < cell.enumNames.Length
                        ? cell.enumNames[cell.enumValueIndex]
                        : string.Empty;
                case SerializedPropertyType.ObjectReference:
                    var obj = cell.objectReferenceValue;
                    return obj == null ? string.Empty : AssetDatabase.GetAssetPath(obj);
                case SerializedPropertyType.Color:
                    return "#" + ColorUtility.ToHtmlStringRGBA(cell.colorValue);
                case SerializedPropertyType.Vector2:
                    var v2 = cell.vector2Value;
                    return Join(v2.x, v2.y);
                case SerializedPropertyType.Vector3:
                    var v3 = cell.vector3Value;
                    return Join(v3.x, v3.y, v3.z);
                default:
                    return null;
            }
        }

        /// <summary>Writes cell text back. False means the type or the text was not usable.</summary>
        public static bool WriteCell(SerializedProperty cell, string text)
        {
            if (cell == null || text == null) return false;
            text = text.Trim();

            switch (cell.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        return false;
                    cell.longValue = l;
                    return true;

                case SerializedPropertyType.Boolean:
                    if (bool.TryParse(text, out bool b)) { cell.boolValue = b; return true; }
                    if (text == "1") { cell.boolValue = true; return true; }
                    if (text == "0") { cell.boolValue = false; return true; }
                    return false;

                case SerializedPropertyType.Float:
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return false;
                    cell.doubleValue = d;
                    return true;

                case SerializedPropertyType.String:
                    cell.stringValue = text;
                    return true;

                case SerializedPropertyType.Enum:
                    string[] names = cell.enumNames;
                    for (int i = 0; i < names.Length; i++)
                        if (string.Equals(names[i], text, StringComparison.OrdinalIgnoreCase))
                        {
                            cell.enumValueIndex = i;
                            return true;
                        }
                    // Fall back to the raw index, which is what the enum actually stores.
                    if (int.TryParse(text, out int idx) && idx >= 0 && idx < names.Length)
                    {
                        cell.enumValueIndex = idx;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.ObjectReference:
                    if (text.Length == 0) { cell.objectReferenceValue = null; return true; }
                    var loaded = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(text);
                    if (loaded == null) return false;   // scene objects have no asset path
                    cell.objectReferenceValue = loaded;
                    return true;

                case SerializedPropertyType.Color:
                    if (!ColorUtility.TryParseHtmlString(text, out Color c)) return false;
                    cell.colorValue = c;
                    return true;

                case SerializedPropertyType.Vector2:
                    if (!TryParseFloats(text, 2, out float[] f2)) return false;
                    cell.vector2Value = new Vector2(f2[0], f2[1]);
                    return true;

                case SerializedPropertyType.Vector3:
                    if (!TryParseFloats(text, 3, out float[] f3)) return false;
                    cell.vector3Value = new Vector3(f3[0], f3[1], f3[2]);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// TSV for the given row indices. A null entry in columnPaths means the row itself
        /// is the value. Pass headers to prefix a header line.
        /// </summary>
        public static string ToTsv(
            SerializedProperty rows,
            IReadOnlyList<int> indices,
            IReadOnlyList<string> columnPaths,
            IReadOnlyList<string> headers = null)
        {
            if (rows == null || !rows.isArray || columnPaths == null) return string.Empty;

            var sb = new StringBuilder();

            if (headers != null && headers.Count > 0)
                AppendLine(sb, headers);

            foreach (int index in indices)
            {
                if (index < 0 || index >= rows.arraySize) continue;

                SerializedProperty row = rows.GetArrayElementAtIndex(index);
                var cells = new List<string>(columnPaths.Count);
                foreach (string path in columnPaths)
                {
                    SerializedProperty cell = path == null ? row : row.FindPropertyRelative(path);
                    cells.Add(ReadCell(cell) ?? string.Empty);
                }
                AppendLine(sb, cells);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes TSV into rows from startIndex on, growing the array when needed.
        /// A first line matching the headers is treated as a header and skipped.
        /// Returns how many cells were written; skipped counts the unusable ones.
        /// </summary>
        public static int Paste(
            SerializedProperty rows,
            int startIndex,
            IReadOnlyList<string> columnPaths,
            string tsv,
            IReadOnlyList<string> headers,
            out int skipped)
        {
            skipped = 0;
            if (rows == null || !rows.isArray || columnPaths == null || string.IsNullOrEmpty(tsv))
                return 0;

            List<List<string>> table = ParseTsv(tsv);
            if (table.Count == 0) return 0;

            if (headers != null && headers.Count > 0 && LooksLikeHeader(table[0], headers))
                table.RemoveAt(0);

            if (startIndex < 0) startIndex = 0;
            int written = 0;

            for (int r = 0; r < table.Count; r++)
            {
                int target = startIndex + r;
                if (target >= rows.arraySize)
                    rows.arraySize = target + 1;

                SerializedProperty row = rows.GetArrayElementAtIndex(target);
                List<string> line = table[r];

                for (int c = 0; c < columnPaths.Count && c < line.Count; c++)
                {
                    string path = columnPaths[c];
                    SerializedProperty cell = path == null ? row : row.FindPropertyRelative(path);
                    if (WriteCell(cell, line[c])) written++;
                    else skipped++;
                }
            }

            return written;
        }

        /// <summary>Splits TSV into rows and cells, honouring Excel style double-quoting.</summary>
        public static List<List<string>> ParseTsv(string tsv)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(tsv)) return rows;

            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < tsv.Length; i++)
            {
                char ch = tsv[i];

                if (quoted)
                {
                    if (ch != DQUOTE) { cell.Append(ch); continue; }
                    if (i + 1 < tsv.Length && tsv[i + 1] == DQUOTE) { cell.Append(DQUOTE); i++; continue; }
                    quoted = false;
                    continue;
                }

                if (ch == DQUOTE) { quoted = true; continue; }
                if (ch == TAB) { row.Add(cell.ToString()); cell.Clear(); continue; }
                if (ch == CR) continue;
                if (ch == LF)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(ch);
            }

            // A trailing newline leaves an empty pending row, which is not a row.
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static bool LooksLikeHeader(IReadOnlyList<string> line, IReadOnlyList<string> headers)
        {
            int compare = Math.Min(line.Count, headers.Count);
            if (compare == 0) return false;

            for (int i = 0; i < compare; i++)
                if (!string.Equals(line[i].Trim(), headers[i], StringComparison.OrdinalIgnoreCase))
                    return false;

            return true;
        }

        private static void AppendLine(StringBuilder sb, IReadOnlyList<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(TAB);
                sb.Append(Quote(cells[i]));
            }
            sb.Append(LF);
        }

        // A tab or newline inside a value would otherwise silently split the row.
        private static string Quote(string value)
        {
            if (value == null) return string.Empty;
            if (value.IndexOf(TAB) < 0 && value.IndexOf(LF) < 0
                && value.IndexOf(CR) < 0 && value.IndexOf(DQUOTE) < 0)
                return value;

            return DQUOTE + value.Replace("\"", "\"\"") + DQUOTE;
        }

        private static string Join(params float[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                parts[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(",", parts);
        }

        private static bool TryParseFloats(string text, int count, out float[] values)
        {
            values = new float[count];
            string[] parts = text.Split(',');
            if (parts.Length != count) return false;

            for (int i = 0; i < count; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;

            return true;
        }
    }
}
