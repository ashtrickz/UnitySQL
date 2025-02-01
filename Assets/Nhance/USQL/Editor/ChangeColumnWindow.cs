using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class ChangeColumnWindow : EditorWindow
{
    private static UnitySQLManager manager;
    private static Database database;
    private static string tableName;
    private static string columnName;
    private static string newColumnName;
    private static int selectedTypeIndex;

    private static readonly string[] columnTypes = { "TEXT", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite" };
    private static string currentColumnType;

    public static void ShowWindow(UnitySQLManager mgr, Database db, string tblName, string colName)
    {
        manager = mgr;
        database = db;
        tableName = tblName;
        columnName = colName;
        newColumnName = colName;

        currentColumnType = database.GetColumnType(tableName, columnName);
        selectedTypeIndex = System.Array.IndexOf(columnTypes, currentColumnType);

        ChangeColumnWindow window = GetWindow<ChangeColumnWindow>("Change Column");
        window.minSize = new Vector2(300, 200);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Change Column: {columnName}", EditorStyles.boldLabel);
        newColumnName = EditorGUILayout.TextField("Column Name:", newColumnName);
        selectedTypeIndex = EditorGUILayout.Popup("Column Type:", selectedTypeIndex, columnTypes);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save"))
        {
            database.ChangeColumnType(tableName, columnName, newColumnName, columnTypes[selectedTypeIndex]);
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