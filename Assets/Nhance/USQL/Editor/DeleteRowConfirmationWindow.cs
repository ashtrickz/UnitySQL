using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class DeleteRowConfirmationWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static Dictionary<string, object> rowData;

    public static void ShowWindow(Database db, string tblName, Dictionary<string, object> row)
    {
        database = db;
        tableName = tblName;
        rowData = row;

        DeleteRowConfirmationWindow window = GetWindow<DeleteRowConfirmationWindow>("Confirm Delete");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 120);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Are you sure you want to delete this row?", EditorStyles.boldLabel);
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
        if (database != null && rowData != null)
        {
            database.DeleteRowFromTable_Maria(tableName, rowData);
            Debug.Log($"[SUCCESS] Deleted row from {tableName}");
            
            database.LoadTableContent(tableName);
        }
        else
        {
            Debug.LogError("[ERROR] Database or rowData is null! Cannot delete row.");
        }

        Close();
    }
}