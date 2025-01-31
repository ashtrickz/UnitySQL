using UnityEngine;

public class GameObjectData
{
    public string name;
    public string prefabPath;
    public Vector3 position;
    public Vector3 rotation;

    public GameObjectData(GameObject go)
    {
        name = go.name;
        prefabPath = "Assets/Prefabs/" + go.name + ".prefab"; // Optional: Store prefab path
        position = go.transform.position;
        rotation = go.transform.eulerAngles;
    }
}