using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class ChangeValueWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static Dictionary<string, object> rowData;
    private static string columnName;
    private static string cellValue;
    private static string newValue;

    public static void ShowWindow(Database db, string tblName, Dictionary<string, object> row, string colName,
        string oldValue)
    {
        database = db;
        tableName = tblName;
        rowData = row;
        columnName = colName;
        cellValue = oldValue;
        newValue = oldValue;

        ChangeValueWindow window = GetWindow<ChangeValueWindow>("Edit Value");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 120);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Edit Value for {columnName}", EditorStyles.boldLabel);
        newValue = EditorGUILayout.TextField("New Value:", newValue);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save"))
        {
            ApplyChange();
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ApplyChange()
    {
        database.UpdateCellValue(tableName, rowData, columnName, newValue);
        database.LoadTableContent(tableName);
        Close();
    }
}