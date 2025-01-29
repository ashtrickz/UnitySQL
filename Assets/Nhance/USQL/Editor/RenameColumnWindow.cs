using UnityEditor;
using UnityEngine;

public class RenameColumnWindow : EditorWindow
{
    private static UnitySQLManager manager;
    private static Database database;
    private static string tableName;
    private static string oldColumnName;
    private static string newColumnName;

    public static void ShowWindow(UnitySQLManager mgr, Database db, string tblName, string oldColName)
    {
        manager = mgr;
        database = db;
        tableName = tblName;
        oldColumnName = oldColName;
        newColumnName = oldColName; // Pre-fill with existing column name

        RenameColumnWindow window = GetWindow<RenameColumnWindow>("Rename Column");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 150);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Rename Column in Table: {tableName}", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Old Column Name:", oldColumnName);
        newColumnName = EditorGUILayout.TextField("New Column Name:", newColumnName);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Rename"))
        {
            if (!string.IsNullOrEmpty(newColumnName) && oldColumnName != newColumnName)
            {
                database.RenameColumnInTable(tableName, oldColumnName, newColumnName);
                Debug.Log($"Renamed column: {oldColumnName} → {newColumnName} in table {tableName}");
                
                database.LoadTableContent(tableName);
                
                Close();
            }
            else
            {
                Debug.LogError("Invalid column name.");
            }
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}