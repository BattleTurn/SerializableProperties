using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BattleTurn.Serializable.Runtime
{
    [Serializable]
    public class SerializableInterface<T> where T : class
    {
        [SerializeField] private UnityEngine.Object unityObject;

        [SerializeReference] private T managed;

        [NonSerialized] private T runtime;

        [SerializeField, HideInInspector]
        private string typeName;

        public Type SelectedType =>
            string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);

        public T Value
        {
            get => runtime ?? (unityObject as T ?? managed);
            set => Set(value);
        }

        public SerializableInterface(T value)
        {
            if (value is UnityEngine.Object unityObj)
            {
                unityObject = unityObj;
                managed = default;
            }
            else
            {
                managed = value;
                unityObject = null;
            }
        }

        public static implicit operator T(SerializableInterface<T> serializableInterface)
        {
            return serializableInterface.Value;
        }

        private void Set(T value)
        {
            runtime = value;

#if UNITY_EDITOR
            SyncSerializedType(value);

            // ⭐ FORCE: Send Editor have changed
#if UNITY_6000_3_OR_NEWER
                EditorUtility.SetDirty(EditorUtility.EntityIdToObject(
                    Selection.activeEntityId
                ));
#else
                EditorUtility.SetDirty(EditorUtility.InstanceIDToObject(
                    UnityEditor.Selection.activeInstanceID
                ));
#endif
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
        }

#if UNITY_EDITOR
        private void SyncSerializedType(T value)
        {
            if (value == null)
            {
                unityObject = null;
                managed = null;
                typeName = null;
                return;
            }

            var t = value.GetType();
            typeName = t.AssemblyQualifiedName; // ⭐ THIẾU DÒNG NÀY

            if (value is UnityEngine.Object uo)
            {
                unityObject = uo;
                managed = null;
            }
            else
            {
                unityObject = null;
                if (managed == null || managed.GetType() != t)
                    managed = (T)Activator.CreateInstance(t);
            }
        }
#endif
    }
}
