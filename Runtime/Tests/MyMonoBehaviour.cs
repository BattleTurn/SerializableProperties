using System;
using UnityEngine;

namespace BattleTurn.Serializable.Tests
{
    public class MyMonoBehaviour : MonoBehaviour, IMyInterface
    {
        [SerializeField] private int myValue;

        public void MyMethod()
        {
            throw new NotImplementedException();
        }
    }
}