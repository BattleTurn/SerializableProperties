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
        [SerializeField] private SerializedGeneric<IMyInterface> myValue;

        public void MyMethod()
        {
            Debug.Log("MyMethod called");
        }
    }

    public class InterfacePropertiesTest : MonoBehaviour
    {
        public MyNestedClass myNestedClass;
        public MyMonoBehaviour myMonoBehaviour;
        public SerializedGeneric<IMyInterface> myInterfaceField;
        public List<SerializedGeneric<IMyInterface>> myInterfaceList;

        public void Start()
        {
            myInterfaceField.Value = myMonoBehaviour;
        }
    }
}
