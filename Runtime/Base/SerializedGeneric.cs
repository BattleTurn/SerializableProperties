using System;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    [Serializable]
    public class SerializedGeneric<T> where T : class
    {
        [SerializeField] private UnityEngine.Object unityObject;

        [SerializeReference] private T managed;

        [NonSerialized] private T runtime;

        [SerializeField, HideInInspector]
        private string typeName;

        public Type Type => string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);

        public string TypeName => typeName;

        public T Value
        {
            get => runtime ?? (unityObject as T ?? managed);
            set => Set(value);
        }

        public SerializedGeneric(T value)
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

        public static implicit operator T(SerializedGeneric<T> serializableInterface)
        {
            return serializableInterface.Value;
        }

        private void Set(T value)
        {
            runtime = value;

#if UNITY_EDITOR
            SyncSerializedType(value);
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
            typeName = t.AssemblyQualifiedName;

            if (value is UnityEngine.Object uo)
            {
                unityObject = uo;
                managed = null;
            }
            else
            {
                unityObject = null;
                managed = value;
            }
        }
#endif
    }
}
