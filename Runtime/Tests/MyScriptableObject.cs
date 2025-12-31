using UnityEngine;

namespace BattleTurn.Serializable.Tests
{
    [CreateAssetMenu(fileName = "MyScriptableObject", menuName = "SerializableProperties/Tests/MyScriptableObject")]
    public class MyScriptableObject : ScriptableObject, IMyInterface
    {
        public void MyMethod()
        {
            throw new System.NotImplementedException();
        }
    }
}
