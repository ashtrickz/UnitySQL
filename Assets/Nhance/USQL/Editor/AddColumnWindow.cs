using UnityEditor;
using UnityEngine;

public class AddColumnWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static string newColumnName = "";
    private static int selectedColumnTypeIndex = 0;
    private static readonly string[] columnTypes = { "TEXT", "INTEGER", "REAL", "BLOB" };

    public static void ShowWindow(Database db, string tblName)
    {
        database = db;
        tableName = tblName;
        newColumnName = ""; // Reset input field

        AddColumnWindow window = GetWindow<AddColumnWindow>("Add Column");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 150);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Add Column to Table: {tableName}", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        newColumnName = EditorGUILayout.TextField("Column Name:", newColumnName);
        selectedColumnTypeIndex = EditorGUILayout.Popup("Column Type:", selectedColumnTypeIndex, columnTypes);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Column"))
        {
            if (!string.IsNullOrEmpty(newColumnName))
            {
                string selectedColumnType = columnTypes[selectedColumnTypeIndex];
                database.AddColumnToTable(tableName, newColumnName, selectedColumnType);
                Debug.Log($"Added column: {newColumnName} ({selectedColumnType}) to table {tableName}");

                database.LoadTableContent(tableName);
                
                Close();
            }
            else
            {
                Debug.LogError("Column name cannot be empty.");
            }
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}