using System;
using UnityEngine;

namespace BattleTurn.Serializable.Runtime
{
    [Serializable]
    public class SerializableInterface<T> where T : class
    {
        [SerializeField] private UnityEngine.Object unityObject;
        [SerializeReference] private T managed;

        public T Value
        {
            get
            {
                if (managed != null)
                    return managed;

                return unityObject as T;
            }
        }

        public UnityEngine.Object UnityObject => unityObject;
        public T Managed => managed;

        public void SetUnityObject(UnityEngine.Object obj)
        {
            unityObject = obj;
            managed = null;
        }

        public void SetManaged(T value)
        {
            managed = value;
            unityObject = null;
        }

        public void Clear()
        {
            unityObject = null;
            managed = null;
        }
    }
}