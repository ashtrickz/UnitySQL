using UnityEngine;

namespace DatabaseManager.Data
{
    public class GameObjectData
    {
        public string Name;
        public string PrefabPath;
        public Vector3 Position;
        public Vector3 Rotation;

        public GameObjectData(GameObject go)
        {
            Name = go.name;
            PrefabPath = "Assets/Prefabs/" + go.name + ".prefab"; // Optional: Store prefab path
            Position = go.transform.position;
            Rotation = go.transform.eulerAngles;
        }
    }
}