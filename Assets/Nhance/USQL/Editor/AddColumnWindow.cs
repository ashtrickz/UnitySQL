using UnityEditor;
using UnityEngine;

public class AddColumnWindow : EditorWindow
{
    private static UnitySQLManager manager;
    private static string tableName;
    private static string columnName = "";
    private static int selectedTypeIndex = 0;
    
    private static readonly string[] columnTypes = { "TEXT", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3" };

    public static void ShowWindow(UnitySQLManager mgr, string tblName)
    {
        manager = mgr;
        tableName = tblName;

        AddColumnWindow window = GetWindow<AddColumnWindow>("Add Column");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 150);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Add New Column", EditorStyles.boldLabel);
        columnName = EditorGUILayout.TextField("Column Name:", columnName);
        selectedTypeIndex = EditorGUILayout.Popup("Column Type:", selectedTypeIndex, columnTypes);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(columnName))
            {
                string selectedType = columnTypes[selectedTypeIndex];

                manager.AddColumnToTable(tableName, columnName, selectedType);
                manager.SaveSessionData();
                Close(); // Close the window after adding the column
            }
            else
            {
                Debug.LogError("[ERROR] Column name cannot be empty.");
            }
        }

        if (GUILayout.Button("Cancel", GUILayout.Height(25)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}