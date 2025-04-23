using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class CreateTableWindow : EditorWindow
{
    private static Database database;
    private static string tableName = "";
    private static List<Database.ColumnDefinition> columns = new List<Database.ColumnDefinition>();

    private static readonly string[] columnTypes = { "TEXT", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3" };
    private static int primaryKeyIndex = -1; // Index of the selected primary key column

    public static void ShowWindow(Database db)
    {
        database = db;
        tableName = "";
        columns.Clear();
        primaryKeyIndex = -1;

        CreateTableWindow window = GetWindow<CreateTableWindow>("Create Table");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 350);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Create New Table", EditorStyles.boldLabel);
        tableName = EditorGUILayout.TextField("Table Name:", tableName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Columns:", EditorStyles.boldLabel);

        // **Draw column list**
        for (int i = 0; i < columns.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            columns[i].Name = EditorGUILayout.TextField(columns[i].Name, GUILayout.Width(150));
            columns[i].Type = columnTypes[EditorGUILayout.Popup(System.Array.IndexOf(columnTypes, columns[i].Type), columnTypes, GUILayout.Width(100))];

            // **Primary Key Checkbox**
            EditorGUILayout.LabelField("Is Primary Key: ", GUILayout.Width(90));
            bool isPrimaryKey = (primaryKeyIndex == i);
            bool newPrimaryKey = EditorGUILayout.Toggle(isPrimaryKey, GUILayout.Width(20));

            if (newPrimaryKey && !isPrimaryKey)
            {
                primaryKeyIndex = i; // Only one primary key allowed
            }
            else if (!newPrimaryKey && isPrimaryKey)
            {
                primaryKeyIndex = -1; // Remove primary key selection
            }

            if (GUILayout.Button("x", GUILayout.Width(25)))
            {
                if (primaryKeyIndex == i) primaryKeyIndex = -1; // Reset primary key if deleted
                columns.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(25))) // **Modified Add Column Button**
        {
            columns.Add(new Database.ColumnDefinition { Name = "", Type = "TEXT" });
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create Table", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(tableName) && columns.Count > 0)
            {
                database.CreateTable_Maria(tableName, columns, primaryKeyIndex);
                Close();
            }
            else
            {
                Debug.LogError("[ERROR] Table name and at least one column are required.");
            }
        }

        if (GUILayout.Button("Cancel", GUILayout.Height(25)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}
