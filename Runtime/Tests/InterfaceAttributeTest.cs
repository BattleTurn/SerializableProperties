using System;
using UnityEngine;
using BattleTurn.Serializable.Runtime;
using System.Collections.Generic;

namespace BattleTurn.Serializable.Tests
{
    public interface IMyInterface
    {
        void MyMethod();
    }

    public interface IMyOtherInterface : IMyInterface
    {
        void MyOtherMethod();
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
    public class MyClass2 : MyClass
    {
        [SerializeField] private string myString;
    }

    [Serializable]
    public class MyOtherClass : IMyOtherInterface
    {
        [SerializeField] private float myFloat;

        public void MyMethod()
        {
            Debug.Log("MyMethod called");
        }

        public void MyOtherMethod()
        {
            Debug.Log("MyOtherMethod called");
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
        public MyMonoBehaviour myMonoBehaviour;
        public SerializableInterface<IMyInterface> myInterfaceField;
        public List<SerializableInterface<IMyInterface>> myInterfaceList;

        public void Start()
        {
            myInterfaceField.Value = myMonoBehaviour;
        }
    }
}