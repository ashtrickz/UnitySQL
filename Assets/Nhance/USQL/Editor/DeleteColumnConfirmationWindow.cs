using UnityEditor;
using UnityEngine;

public class DeleteColumnConfirmationWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static string columnName;

    public static void ShowWindow(Database db, string tblName, string colName)
    {
        database = db;
        tableName = tblName;
        columnName = colName;

        DeleteColumnConfirmationWindow window = GetWindow<DeleteColumnConfirmationWindow>("Confirm Delete Column");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 120);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Are you sure you want to delete column '{columnName}'?", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Yes"))
        {
            ConfirmDelete();
        }

        if (GUILayout.Button("No"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ConfirmDelete()
    {
        if (database != null && !string.IsNullOrEmpty(columnName))
        {
            database.RemoveColumnFromTable(tableName, columnName);
            Debug.Log($"[SUCCESS] Deleted column: {columnName} from table: {tableName}");
        }
        else
        {
            Debug.LogError("[ERROR] Database or column name is null! Cannot delete column.");
        }

        Close();
    }
}