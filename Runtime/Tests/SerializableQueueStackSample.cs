using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public class SerializableQueueStackSample : MonoBehaviour
    {
        public SerializableQueue<int> intQueue;
        public SerializableStack<string> stringStack;
    }
}
