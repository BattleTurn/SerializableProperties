using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Editor
{
    [CustomPropertyDrawer(typeof(SerializableInterface<>), true)]
    public class SerializableInterfaceDrawer : PropertyDrawer
    {
        // 🔑 Popup state – SỐNG RIÊNG
        static Dictionary<string, int> popupIndexCache = new();

        Type[] types;
        string[] names;
        bool inited;

        void Init(Type iface)
        {
            if (inited) return;

            types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    iface.IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    !t.IsInterface)
                .ToArray();

            names = new string[types.Length + 1];
            names[0] = "None";
            for (int i = 0; i < types.Length; i++)
                names[i + 1] = types[i].Name;

            inited = true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;

            // Ensure types/names are initialized – GetPropertyHeight is called before OnGUI
            try
            {
                var fieldType = fieldInfo?.FieldType;
                if (fieldType != null && fieldType.IsGenericType)
                {
                    var iface = fieldType.GetGenericArguments()[0];
                    Init(iface);
                }
            }
            catch { /* best-effort init; fall back to minimal height */ }

            int index = GetPopupIndex(property);
            if (index <= 0) return h;

            h += EditorGUIUtility.standardVerticalSpacing;

            // Clamp index against discovered types
            if (types == null || index - 1 < 0 || index - 1 >= types.Length)
                return h; // fallback: header only

            if (typeof(UnityEngine.Object).IsAssignableFrom(types[index - 1]))
            {
                h += EditorGUIUtility.singleLineHeight;
            }
            else
            {
                var managed = property.FindPropertyRelative("managed");
                h += GetManagedHeight(managed);
            }

            return h;
        }

        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            var iface = fieldInfo.FieldType.GetGenericArguments()[0];
            Init(iface);

            var unityObj = property.FindPropertyRelative("unityObject");
            var managed = property.FindPropertyRelative("managed");

            EditorGUI.BeginProperty(pos, label, property);

            // HEADER
            Rect header = new Rect(pos.x, pos.y, pos.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(header, label);

            Rect popup = header;
            popup.xMin += EditorGUIUtility.labelWidth;

            string key = property.propertyPath;
            int index = GetPopupIndex(property);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popup, index, names);
            if (EditorGUI.EndChangeCheck())
            {
                popupIndexCache[key] = newIndex;

                if (newIndex == 0)
                {
                    unityObj.objectReferenceValue = null;
                    managed.managedReferenceValue = null;
                }
                else
                {
                    Type t = types[newIndex - 1];
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(t))
                    {
                        managed.managedReferenceValue = Activator.CreateInstance(t);
                        unityObj.objectReferenceValue = null;
                    }
                    // ⚠️ UnityObject: KHÔNG clear gì cả
                }
            }

            // BODY
            if (newIndex > 0)
            {
                float y = pos.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                Type t = types[newIndex - 1];

                if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                {
                    Rect r = new Rect(
                        pos.x + EditorGUIUtility.labelWidth,
                        y,
                        pos.width - EditorGUIUtility.labelWidth,
                        EditorGUIUtility.singleLineHeight
                    );

                    unityObj.objectReferenceValue = EditorGUI.ObjectField(
                        r,
                        unityObj.objectReferenceValue,
                        t,
                        true
                    );
                }
                else
                {
                    DrawManaged(pos, y, managed);
                }
            }

            EditorGUI.EndProperty();
        }

        int GetPopupIndex(SerializedProperty property)
        {
            string key = property.propertyPath;

            if (popupIndexCache.TryGetValue(key, out int i))
                return i;
            // Ensure initialization when height is queried before OnGUI
            try
            {
                var fieldType = fieldInfo?.FieldType;
                if (fieldType != null && fieldType.IsGenericType)
                {
                    var iface = fieldType.GetGenericArguments()[0];
                    Init(iface);
                }
            }
            catch { }

            var unityObj = property.FindPropertyRelative("unityObject");
            var managed = property.FindPropertyRelative("managed");

            if (managed != null && managed.managedReferenceValue != null)
            {
                int idx = Array.IndexOf(types ?? Array.Empty<Type>(), managed.managedReferenceValue.GetType());
                return popupIndexCache[key] = idx >= 0 ? idx + 1 : 0;
            }

            if (unityObj != null && unityObj.objectReferenceValue != null)
            {
                int idx = Array.IndexOf(types ?? Array.Empty<Type>(), unityObj.objectReferenceValue.GetType());
                return popupIndexCache[key] = idx >= 0 ? idx + 1 : 0;
            }

            return 0;
        }

        void DrawManaged(Rect pos, float y, SerializedProperty managed)
        {
            if (managed == null)
                return;

            SerializedProperty it = managed.Copy();
            SerializedProperty end = it.GetEndProperty();
            it.NextVisible(true);

            EditorGUI.indentLevel++;
            while (!SerializedProperty.EqualContents(it, end))
            {
                float h = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), it, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                it.NextVisible(false);
            }
            EditorGUI.indentLevel--;
        }

        float GetManagedHeight(SerializedProperty managed)
        {
            if (managed == null)
                return 0f;

            float h = 0;
            SerializedProperty it = managed.Copy();
            SerializedProperty end = it.GetEndProperty();
            it.NextVisible(true);

            while (!SerializedProperty.EqualContents(it, end))
            {
                h += EditorGUI.GetPropertyHeight(it, true)
                   + EditorGUIUtility.standardVerticalSpacing;
                it.NextVisible(false);
            }
            return h;
        }
    }
}
