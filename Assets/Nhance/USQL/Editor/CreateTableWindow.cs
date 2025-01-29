using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class CreateTableWindow : EditorWindow
{
    private static Database database;
    private static string tableName = "";
    private static List<string> columnNames = new List<string>() { "id" };
    private static List<string> columnTypes = new List<string>() { "INTEGER" };
    private static List<bool> isPrimaryKey = new List<bool>() { true }; // Default first column as primary key
    private static readonly string[] availableColumnTypes = { "TEXT", "INTEGER", "REAL", "BLOB" };

    public static void ShowWindow(Database db)
    {
        database = db;
        tableName = "";
        columnNames = new List<string>() { "id" };
        columnTypes = new List<string>() { "INTEGER" };
        isPrimaryKey = new List<bool>() { true };

        CreateTableWindow window = GetWindow<CreateTableWindow>("Create New Table", true);
        window.minSize = new Vector2(450, 350);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Create a New Table in {database.Name}", EditorStyles.boldLabel);
        
        tableName = EditorGUILayout.TextField("Table Name:", tableName);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Columns:", EditorStyles.boldLabel);

        if (columnNames == null || columnTypes == null || isPrimaryKey == null)
        {
            columnNames = new List<string> { "id" };
            columnTypes = new List<string> { "INTEGER" };
            isPrimaryKey = new List<bool> { true };
        }

        for (int i = 0; i < columnNames.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            if (i >= columnNames.Count || i >= columnTypes.Count || i >= isPrimaryKey.Count)
            {
                Debug.LogError("[ERROR] Column list index out of bounds.");
                break;
            }

            columnNames[i] = EditorGUILayout.TextField(columnNames[i], GUILayout.Width(200));
            columnTypes[i] = availableColumnTypes[
                EditorGUILayout.Popup(
                    Mathf.Clamp(System.Array.IndexOf(availableColumnTypes, columnTypes[i]), 0, availableColumnTypes.Length - 1),
                    availableColumnTypes, GUILayout.Width(150)
                )
            ];

            EditorGUILayout.LabelField("Is Primary Key: ", GUILayout.Width(90));
            bool currentPrimaryKey = EditorGUILayout.Toggle(isPrimaryKey[i], GUILayout.Width(20));

            if (currentPrimaryKey && !isPrimaryKey[i]) // If the checkbox was just selected
            {
                for (int j = 0; j < isPrimaryKey.Count; j++)
                {
                    isPrimaryKey[j] = (j == i); // Only one can be true
                }
            }

            isPrimaryKey[i] = currentPrimaryKey;

            if (GUILayout.Button("x", GUILayout.Width(25)) && columnNames.Count > 1)
            {
                columnNames.RemoveAt(i);
                columnTypes.RemoveAt(i);
                isPrimaryKey.RemoveAt(i);

                if (!isPrimaryKey.Contains(true) && columnNames.Count > 0) // Ensure at least one primary key
                {
                    isPrimaryKey[0] = true;
                }
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+", GUILayout.Width(25)))
        {
            columnNames.Add("new_column");
            columnTypes.Add("TEXT");
            isPrimaryKey.Add(false);
        }

        EditorGUILayout.Space(15);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Table", GUILayout.Width(150)))
        {
            CreateTable();
        }
        if (GUILayout.Button("Cancel", GUILayout.Width(150)))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void CreateTable()
    {
        if (string.IsNullOrEmpty(tableName))
        {
            Debug.LogError("[ERROR] Table name cannot be empty.");
            return;
        }

        int primaryKeyIndex = isPrimaryKey.IndexOf(true);
        if (primaryKeyIndex == -1)
        {
            Debug.LogError("[ERROR] No primary key selected. Please choose a primary key column.");
            return;
        }

        // Ensure the primary key column has "PRIMARY KEY" in its type
        columnTypes[primaryKeyIndex] += " PRIMARY KEY";

        database.CreateTable(tableName, columnNames, columnTypes);
        Close();
    }
}
