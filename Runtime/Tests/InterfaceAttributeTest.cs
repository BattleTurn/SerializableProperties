using System;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public interface IMyInterface
    {
        void MyMethod();
    }

    [Serializable]
    public class MyClass : IMyInterface
    {
        [SerializeField] private int myValue;

        public void MyMethod()
        {
            Debug.Log("MyMethod called");
        }
    }

    [Serializable]
    public class MyNestedClass : IMyInterface
    {
        [SerializeField] private SerializableInterface<IMyInterface> myValue;

        public void MyMethod()
        {
            Debug.Log("MyMethod called");
        }
    }

    public class InterfaceAttributeTest : MonoBehaviour
    {
        public MyNestedClass myNestedClass;
        public SerializableInterface<IMyInterface> myInterfaceField;
    }
}