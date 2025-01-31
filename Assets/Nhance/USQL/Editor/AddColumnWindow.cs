using UnityEditor;
using UnityEngine;

public class AddColumnWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static string newColumnName = "";
    private static int selectedColumnTypeIndex = 0; // Default type is TEXT
    private static readonly string[] availableColumnTypes = { "TEXT", "INTEGER", "REAL", "BLOB", "Vector2", "Vector3", "Sprite", "GameObject" };


    public static void ShowWindow(Database db, string tblName)
    {
        database = db;
        tableName = tblName;
        newColumnName = "";
        selectedColumnTypeIndex = 0;

        AddColumnWindow window = GetWindow<AddColumnWindow>("Add Column", true);
        window.minSize = new Vector2(300, 150);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Add Column to {tableName}", EditorStyles.boldLabel);

        newColumnName = EditorGUILayout.TextField("Column Name:", newColumnName);

        EditorGUILayout.Space(5);

        // Column Type Dropdown
        selectedColumnTypeIndex = EditorGUILayout.Popup("Column Type:", selectedColumnTypeIndex, availableColumnTypes);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Column", GUILayout.Width(120)))
        {
            AddColumn();
        }
        if (GUILayout.Button("Cancel", GUILayout.Width(120)))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void AddColumn()
    {
        if (string.IsNullOrEmpty(newColumnName))
        {
            Debug.LogError("[ERROR] Column name cannot be empty.");
            return;
        }

        string selectedColumnType = availableColumnTypes[selectedColumnTypeIndex];
        database.AddColumnToTable(tableName, newColumnName, selectedColumnType);
        
        database.LoadTableContent(tableName);
        
        Close();
    }
}