using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Editor
{
    [CustomPropertyDrawer(typeof(SerializableInterface<>), true)]
    public class SerializableInterfaceDrawer : PropertyDrawer
    {
        Type[] types;
        string[] names;
        bool inited;

        void Init(Type interfaceType)
        {
            if (inited) return;

            types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); }
                })
                .Where(t =>
                    interfaceType.IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    !t.IsInterface)
                .ToArray();

            names = new string[types.Length + 1];
            names[0] = "None";
            for (int i = 0; i < types.Length; i++)
                names[i + 1] = types[i].Name;

            inited = true;
        }

        // ================= HEIGHT =================

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Init(GetTargetInterfaceType());

            float h = EditorGUIUtility.singleLineHeight;
            int index = GetPopupIndex(property);

            if (index <= 0)
                return h;

            h += EditorGUIUtility.standardVerticalSpacing;

            Type t = types[index - 1];
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                h += EditorGUIUtility.singleLineHeight;
            }
            else
            {
                h += GetManagedHeight(property.FindPropertyRelative("managed"));
            }

            return h;
        }

        // ================= GUI =================

        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            Init(GetTargetInterfaceType());

            var unityObj = property.FindPropertyRelative("unityObject");
            var managed = property.FindPropertyRelative("managed");
            var typeNameProp = property.FindPropertyRelative("typeName");

            ArrayElementHelper.EnsureUniqueManagedInstanceInArray(property, unityObj, managed);


            int index = GetPopupIndex(property);

            EditorGUI.BeginProperty(pos, label, property);

            // ===== HEADER =====
            Rect header = new Rect(pos.x, pos.y, pos.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(header, label);

            Rect popup = header;
            popup.xMin += EditorGUIUtility.labelWidth;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popup, index, names);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyTypeChange(unityObj, managed, typeNameProp, newIndex);
                index = newIndex;
            }

            // ===== BODY =====
            if (index > 0)
            {
                float y = pos.y + EditorGUIUtility.singleLineHeight
                          + EditorGUIUtility.standardVerticalSpacing;

                Type t = types[index - 1];

                if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                {
                    unityObj.objectReferenceValue = EditorGUI.ObjectField(
                        new Rect(
                            pos.x + EditorGUIUtility.labelWidth,
                            y,
                            pos.width - EditorGUIUtility.labelWidth,
                            EditorGUIUtility.singleLineHeight
                        ),
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

        Type GetTargetInterfaceType()
        {
            // Resolve the interface T from various field declarations
            // e.g., List<SerializableInterface<T>>, SerializableInterface<T>, or arrays
            Type t = fieldInfo.FieldType;

            // If List<...>, get element type
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                t = t.GetGenericArguments()[0];

            // If array, get element type
            if (t.IsArray)
                t = t.GetElementType();

            // If SerializableInterface<T>, return T
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SerializableInterface<>))
                return t.GetGenericArguments()[0];

            // Fallback: return as-is
            return t;
        }

        int GetPopupIndex(SerializedProperty property)
        {
            var typeNameProp = property.FindPropertyRelative("typeName");
            if (string.IsNullOrEmpty(typeNameProp.stringValue))
                return 0;

            var t = Type.GetType(typeNameProp.stringValue);
            int idx = Array.IndexOf(types, t);
            return idx >= 0 ? idx + 1 : 0;
        }

        void ApplyTypeChange(
    SerializedProperty unityObj,
    SerializedProperty managed,
    SerializedProperty typeNameProp,
    int index)
        {
            if (index == 0)
            {
                unityObj.objectReferenceValue = null;
                managed.managedReferenceValue = null;
                typeNameProp.stringValue = null;
                return;
            }

            Type t = types[index - 1];
            typeNameProp.stringValue = t.AssemblyQualifiedName;

            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                unityObj.objectReferenceValue = null; // chờ user chọn object
                managed.managedReferenceValue = null;
            }
            else
            {
                unityObj.objectReferenceValue = null;
                managed.managedReferenceValue = Activator.CreateInstance(t);
            }
        }

        void DrawManaged(Rect pos, float y, SerializedProperty managed)
        {
            if (managed == null) return;

            SerializedProperty it = managed.Copy();
            SerializedProperty end = it.GetEndProperty();
            it.NextVisible(true);

            EditorGUI.indentLevel++;
            while (!SerializedProperty.EqualContents(it, end))
            {
                float h = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(
                    new Rect(pos.x, y, pos.width, h),
                    it,
                    true
                );
                y += h + EditorGUIUtility.standardVerticalSpacing;
                it.NextVisible(false);
            }
            EditorGUI.indentLevel--;
        }

        float GetManagedHeight(SerializedProperty managed)
        {
            if (managed == null) return 0f;

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
