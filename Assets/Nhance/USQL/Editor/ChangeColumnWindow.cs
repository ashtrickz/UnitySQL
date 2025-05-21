using UnityEditor;
using UnityEngine;

public class ChangeColumnWindow : EditorWindow
{
    private static Database database;
    private static string tableName;
    private static string columnName;
    private static string newColumnName;
    private static int selectedTypeIndex;

    private static readonly string[] columnTypes = { "TEXT", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3" };
    private static string currentColumnType;

    public static void ShowWindow(Database db, string tblName, string colName)
    {
        database = db;
        tableName = tblName;
        columnName = colName;
        newColumnName = colName;

        currentColumnType = database.ConnectionType == DatabaseConnection.EConnectionType.MySQL
            ? database.GetColumnType_Maria(tableName, columnName)
            : database.GetColumnType_Lite(tableName, columnName);
        
        selectedTypeIndex = System.Array.IndexOf(columnTypes, currentColumnType);
        if (selectedTypeIndex == -1) selectedTypeIndex = 0; // Default to TEXT if unknown type

        ChangeColumnWindow window = GetWindow<ChangeColumnWindow>("Change Column");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 200);
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
            database.ChangeColumnType_Maria(tableName, columnName, newColumnName, columnTypes[selectedTypeIndex]);
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