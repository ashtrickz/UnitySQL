using UnityEditor;
using UnityEngine;

public class CreateDatabaseWindow : EditorWindow
{
    private static UnitySQLManager manager;
    private static DatabaseConnection connection;
    private static string databaseName = "";

    public static void ShowWindow(UnitySQLManager mgr, DatabaseConnection conn)
    {
        manager = mgr;
        connection = conn;

        CreateDatabaseWindow window = GetWindow<CreateDatabaseWindow>("Create Database");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 150);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Create a New Database", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Database Name:", EditorStyles.label);
        databaseName = EditorGUILayout.TextField(databaseName);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(databaseName))
            {
                connection.CreateDatabase(databaseName);
                manager.SaveSessionData();
                Close();
            }
            else
            {
                Debug.LogError("[ERROR] Database name cannot be empty.");
            }
        }

        if (GUILayout.Button("Cancel", GUILayout.Height(25)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}