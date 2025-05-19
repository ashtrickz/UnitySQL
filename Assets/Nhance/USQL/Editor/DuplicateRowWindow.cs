using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Nhance.USQL.Editor;

public class DuplicateRowWindow : EditorWindow
{
    private static UnitySqlManager manager;
    private static string tableName;
    private static Dictionary<string, object> originalRow;
    private static Dictionary<string, object> editedRow;
    private static string primaryKeyColumn;
    private static Database database;

    public static void ShowWindow(UnitySqlManager mgr, string tblName, Dictionary<string, object> row, string primaryKey, Database db)
    {
        manager = mgr;
        tableName = tblName;
        primaryKeyColumn = primaryKey;
        database = db;

        // Copy original row to allow editing
        originalRow = new Dictionary<string, object>(row);
        editedRow = new Dictionary<string, object>(row);

        DuplicateRowWindow window = GetWindow<DuplicateRowWindow>("Duplicate Row");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 300);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Edit Duplicated Row", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Iterate over all columns in the row
        foreach (var column in originalRow.Keys)
        {
            string columnType = database.GetColumnType_Maria(tableName, column);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(column, GUILayout.Width(120));

            // **Primary Key Field (Must Be Unique)**
            if (column == primaryKeyColumn)
            {
                editedRow[column] = EditorGUILayout.TextField(editedRow[column]?.ToString() ?? "");
            }
            // **GameObject & Sprite Property Fields**
            else if (columnType == "GameObject" || columnType == "Sprite")
            {
                System.Type objectType = columnType == "GameObject" ? typeof(GameObject) : typeof(Sprite);
                editedRow[column] = EditorGUILayout.ObjectField(editedRow[column] as UnityEngine.Object, objectType, false);
            }
            // **Numeric Input Fields**
            else if (columnType == "INTEGER" || columnType == "REAL")
            {
                if (float.TryParse(editedRow[column]?.ToString(), out float numericValue))
                {
                    editedRow[column] = EditorGUILayout.FloatField(numericValue);
                }
                else
                {
                    editedRow[column] = EditorGUILayout.FloatField(0);
                }
            }
            // **Default String Input Fields**
            else
            {
                editedRow[column] = EditorGUILayout.TextField(editedRow[column]?.ToString() ?? "");
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Duplicate", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(editedRow[primaryKeyColumn]?.ToString()))
            {
                if (!manager.DoesPrimaryKeyExist(tableName, primaryKeyColumn, editedRow[primaryKeyColumn].ToString()))
                {
                    manager.DuplicateRow(tableName, editedRow);
                    Close();
                }
                else
                {
                    Debug.LogError($"[ERROR] Primary key '{editedRow[primaryKeyColumn]}' already exists in table '{tableName}'.");
                }
            }
            else
            {
                Debug.LogError("[ERROR] Primary key cannot be empty.");
            }
        }

        if (GUILayout.Button("Cancel", GUILayout.Height(25)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}
