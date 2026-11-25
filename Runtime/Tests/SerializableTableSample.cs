using System;
using UnityEngine;
using BattleTurn.Serializable.Runtime;

namespace BattleTurn.Serializable.Tests
{
    public class SerializableTableSample : MonoBehaviour
    {
        [Serializable]
        public class EnemyRow
        {
            public string id;
            public int health;
            public float moveSpeed;
            public KeyCode debugKey;
            public GameObject prefab;
        }

        public SerializableTable<EnemyRow> enemies;
        public SerializableTable<int> plainValues;
    }
}
