using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class AddRowWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static Dictionary<string, string> columnValues = new Dictionary<string, string>();

    public static void ShowWindow(Database db, string tblName, List<string> columnNames)
    {
        database = db;
        tableName = tblName;
        columnValues.Clear();

        foreach (var column in columnNames)
        {
            columnValues[column] = ""; // Initialize input fields as empty
        }

        AddRowWindow window = GetWindow<AddRowWindow>($"Add Row to {tableName}");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 200 + (columnNames.Count * 25));
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Insert Data into {tableName}", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        List<string> keys = new List<string>(columnValues.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];

            if (columnValues[key].StartsWith("Vector2"))
            {
                Vector2 vec2 = Vector2.zero;
                columnValues[key] = EditorGUILayout.Vector2Field(key + ":", vec2).ToString();
            }
            else if (columnValues[key].StartsWith("Vector3"))
            {
                Vector3 vec3 = Vector3.zero;
                columnValues[key] = EditorGUILayout.Vector3Field(key + ":", vec3).ToString();
            }
            else if (columnValues[key].StartsWith("Sprite"))
            {
                Sprite sprite = null;
                sprite = (Sprite)EditorGUILayout.ObjectField(key + ":", sprite, typeof(Sprite), false);
                columnValues[key] = sprite != null ? JsonUtility.ToJson(sprite) : "";
            }
            else if (columnValues[key].StartsWith("GameObject"))
            {
                GameObject gameObject = null;
                gameObject = (GameObject)EditorGUILayout.ObjectField(key + ":", gameObject, typeof(GameObject), false);
                columnValues[key] = gameObject != null ? JsonUtility.ToJson(new GameObjectData(gameObject)) : "";
            }
            else
            {
                columnValues[key] = EditorGUILayout.TextField(key + ":", columnValues[key]);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Row"))
        {
            Dictionary<string, object> rowData = new Dictionary<string, object>();
            foreach (var kvp in columnValues)
            {
                rowData[kvp.Key] = kvp.Value;
            }

            database.InsertIntoTable_Maria(tableName, rowData);
            database.LoadTableContent(tableName);
            Close();
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

}