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
                if (unityObject is T t)
                    return t;
                return managed;
            }
        }
    }
}