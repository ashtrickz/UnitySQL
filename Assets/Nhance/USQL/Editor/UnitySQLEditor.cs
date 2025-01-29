using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Linq;

public class UnitySQLManager : EditorWindow
{
    private List<DatabaseConnection> connections = new List<DatabaseConnection>();
    private int selectedConnectionIndex = -1;
    private int selectedDatabaseIndex = -1;
    private string[] tabs = { "Database Structure", "SQL", "Placeholder" };
    private int selectedTab = 0;
    private string selectedTable;
    private string newEntryData = "";

    private string newColumnName = "";
    private int selectedColumnTypeIndex = 0; // Stores the selected index
    private string selectedTableForColumns = "";
    private string[] availableColumnTypes = { "TEXT", "INTEGER", "REAL", "BLOB" }; // Available column types

    private string selectedTableForContent = "";
    private Vector2 scrollPosition;

    private GenericMenu columnContextMenu;

    [MenuItem("Nhance/Tools/UnitySQL Manager")]
    public static void ShowWindow()
    {
        GetWindow<UnitySQLManager>("Nhance Unity SQL Manager");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        // Left Panel
        DrawConnectionsPanel();

        // Right Panel
        DrawRightPanel();

        EditorGUILayout.EndHorizontal();
    }

    private Dictionary<DatabaseConnection, bool> connectionStates = new Dictionary<DatabaseConnection, bool>();
    private Dictionary<Database, bool> databaseStates = new Dictionary<Database, bool>();

    private void DrawConnectionsPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);

        GUIStyle containerStyle = new GUIStyle("box")
        {
            padding = new RectOffset(5, 5, 5, 5),
            margin = new RectOffset(5, 5, 5, 5)
        };

        for (int i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];

            if (!connectionStates.ContainsKey(connection))
            {
                connectionStates[connection] = false;
            }

            EditorGUILayout.BeginVertical(containerStyle);

            bool isConnectionExpanded = connectionStates[connection];
            string connectionArrow = isConnectionExpanded ? "▼" : "▶";

            if (GUILayout.Button($" {connectionArrow}\t {connection.Name} connection", EditorStyles.boldLabel))
            {
                connectionStates[connection] = !isConnectionExpanded;
            }

            if (isConnectionExpanded)
            {
                foreach (var database in connection.Databases)
                {
                    if (!databaseStates.ContainsKey(database))
                    {
                        databaseStates[database] = false;
                    }

                    bool isDatabaseExpanded = databaseStates[database];
                    string databaseArrow = isDatabaseExpanded ? "▼" : "▶";

                    EditorGUILayout.BeginVertical(containerStyle);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(15);

                    if (GUILayout.Button($" {databaseArrow}\t {database.Name}", EditorStyles.boldLabel,
                            GUILayout.ExpandWidth(true)))
                    {
                        databaseStates[database] = !isDatabaseExpanded;
                        selectedDatabaseIndex = connection.Databases.IndexOf(database);
                        selectedConnectionIndex = i;
                    }

                    // "Refresh Tables" button (Left-Aligned)
                    if (GUILayout.Button("⟳", GUILayout.Width(25)))
                    {
                        database.RefreshTables();
                    }

                    // "+" Button for adding a new table (Right-Aligned)
                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        OpenCreateTableWindow(database);
                    }

                    EditorGUILayout.EndHorizontal();

                    if (isDatabaseExpanded)
                    {
                        foreach (var table in database.Tables)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(30);

                            if (GUILayout.Button($" -\t {table.Name}", EditorStyles.boldLabel))
                            {
                                selectedTableForContent = table.Name;
                                database.LoadTableContent(selectedTableForContent);
                            }

                            if (GUILayout.Button("⟳", GUILayout.Width(25)))
                            {
                                database.LoadTableContent(table.Name);
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical(); // Close connection container
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Add Connection"))
        {
            AddNewConnection();
        }

        EditorGUILayout.EndVertical();
    }


    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(style: "box");

        if (selectedConnectionIndex < 0 || selectedDatabaseIndex < 0)
        {
            EditorGUILayout.LabelField("Select a connection and database to manage.");
            EditorGUILayout.EndVertical();
            return;
        }

        selectedTab = GUILayout.Toolbar(selectedTab, tabs);

        switch (selectedTab)
        {
            case 0:
                DrawDatabaseStructure();
                break;
            case 1:
                DrawSQLExecutor();
                break;
            case 2:
                DrawPlaceholder();
                break;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDatabaseStructure()
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        DrawTableContentUI(database); // Only show table content
    }

    private void OpenCreateTableWindow(Database database)
    {
        CreateTableWindow.ShowWindow(database);
    }

    private void DrawTableContentUI(Database database)
    {
        if (string.IsNullOrEmpty(selectedTableForContent))
        {
            return;
        }

        var table = database.Tables.FirstOrDefault(t => t.Name == selectedTableForContent);
        if (table == null)
        {
            return;
        }

        // Fetch the primary key for the table
        string primaryKeyColumn = database.GetPrimaryKeyColumn(selectedTableForContent);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

        if (table.Data.Count > 0)
        {
            // COLUMN HEADERS with primary key visualization
            EditorGUILayout.BeginHorizontal();
            foreach (var originalColumnName in table.Data[0].Keys)
            {
                GUIStyle columnStyle = new GUIStyle(GUI.skin.button);
                columnStyle.alignment = TextAnchor.MiddleCenter;
                columnStyle.padding = new RectOffset(5, 5, 2, 2);

                string displayColumnName = originalColumnName; // Temporary variable

                // Highlight Primary Key Column
                if (displayColumnName == primaryKeyColumn)
                {
                    columnStyle.normal.textColor = Color.green; // Change text color
                    displayColumnName = $"🔑 {displayColumnName}"; // Add key icon
                }

                Rect columnRect = GUILayoutUtility.GetRect(new GUIContent(displayColumnName), columnStyle,
                    GUILayout.Width(101));

                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                    columnRect.Contains(Event.current.mousePosition))
                {
                    ShowColumnContextMenu(originalColumnName, selectedTableForContent);
                    Event.current.Use();
                }

                GUI.Label(columnRect, displayColumnName, columnStyle);
            }

// "+" button at the end of columns
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                OpenAddColumnWindow(selectedTableForContent);
            }

            EditorGUILayout.EndHorizontal();


            // TABLE ROWS with transparent cell styling
            foreach (var row in table.Data)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var column in row.Keys)
                {
                    string cellValue = row[column]?.ToString() ?? "NULL";

                    GUIStyle cellStyle = new GUIStyle(GUI.skin.box);
                    cellStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 0.1f));
                    cellStyle.alignment = TextAnchor.MiddleLeft;
                    cellStyle.padding = new RectOffset(5, 5, 2, 2);

                    // Highlight Primary Key Cell
                    if (column == primaryKeyColumn)
                    {
                        cellStyle.normal.textColor = Color.green; // Change text color
                        cellValue = $"🔑 {cellValue}"; // Add key icon
                    }

                    Rect cellRect =
                        GUILayoutUtility.GetRect(new GUIContent(cellValue), cellStyle, GUILayout.Width(100));

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        ShowCellContextMenu(selectedTableForContent, row, column, cellValue);
                        Event.current.Use();
                    }

                    GUI.Label(cellRect, cellValue, cellStyle);
                }

                // "..." button to open row context menu
                if (GUILayout.Button("...", GUILayout.Width(25)))
                {
                    ShowRowContextMenu(selectedTableForContent, row);
                }

                EditorGUILayout.EndHorizontal();
            }

            // "+" button below last row (Add New Row)
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                OpenAddRowWindow(selectedTableForContent);
            }
        }
        else
        {
            EditorGUILayout.LabelField("No Data Found.");

            // "+" button below last row (Add New Row)
            if (GUILayout.Button("Add first row (Press only if selected empty table!)"))
            {
                OpenAddRowWindow(selectedTableForContent);
            }
        }

        EditorGUILayout.EndScrollView();
    }


    private void ShowRowContextMenu(string tableName, Dictionary<string, object> rowData)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Duplicate Row"), false, () => DuplicateRow(tableName, rowData));
        menu.AddItem(new GUIContent("Delete Row"), false, () => OpenDeleteRowWindow(tableName, rowData));
        menu.ShowAsContext();
    }

    private void ShowCellContextMenu(string tableName, Dictionary<string, object> rowData, string columnName,
        string cellValue)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Change Value"), false,
            () => OpenChangeValueWindow(tableName, rowData, columnName, cellValue));
        menu.AddItem(new GUIContent("Copy"), false, () => CopyCellToClipboard(cellValue));
        menu.ShowAsContext();
    }


    private void OpenChangeValueWindow(string tableName, Dictionary<string, object> rowData, string columnName,
        string cellValue)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null && rowData != null)
        {
            ChangeValueWindow.ShowWindow(database, tableName, rowData, columnName, cellValue);
        }
        else
        {
            Debug.LogError("[ERROR] Unable to open change value window. Database or row data is null.");
        }
    }


    private void CopyCellToClipboard(string cellValue)
    {
        EditorGUIUtility.systemCopyBuffer = cellValue;
        Debug.Log($"[INFO] Copied value: {cellValue}");
    }


    private void DuplicateRow(string tableName, Dictionary<string, object> rowData)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
        database.DuplicateRowInTable(tableName, rowData);
        database.LoadTableContent(tableName);
    }

    private void OpenDeleteRowWindow(string tableName, Dictionary<string, object> rowData)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null && rowData != null)
        {
            DeleteRowConfirmationWindow.ShowWindow(database, tableName, rowData);
        }
        else
        {
            Debug.LogError("[ERROR] Unable to open delete confirmation window. Database or row data is null.");
        }
    }


    private void OpenAddRowWindow(string tableName)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        // Get column names for this table
        List<string> columnNames = database.GetColumnNames(tableName);

        AddRowWindow.ShowWindow(database, tableName, columnNames);
    }


    private void OpenAddColumnWindow(string tableName)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
        AddColumnWindow.ShowWindow(database, tableName);
    }

    private void ShowColumnContextMenu(string columnName, string tableName)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Rename Column"), false, () => OpenRenameColumnWindow(tableName, columnName));
        menu.AddItem(new GUIContent("Delete Column"), false, () => OpenDeleteColumnWindow(tableName, columnName));
        menu.AddItem(new GUIContent("Make Primary Key"), false, () => MakeColumnPrimaryKey(tableName, columnName));
        menu.ShowAsContext();
    }

    private void OpenDeleteColumnWindow(string tableName, string columnName)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
        DeleteColumnConfirmationWindow.ShowWindow(database, tableName, columnName);
    }


    private void MakeColumnPrimaryKey(string tableName, string columnName)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
        database.MakePrimaryKey(tableName, columnName);
        database.LoadTableContent(tableName);
    }

    private void OpenRenameColumnWindow(string tableName, string columnName)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
        RenameColumnWindow.ShowWindow(this, database, tableName, columnName);
    }

    private void DrawSQLExecutor()
    {
        EditorGUILayout.LabelField("SQL Query Executor", EditorStyles.boldLabel);
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        database.SQLQuery = EditorGUILayout.TextArea(database.SQLQuery, GUILayout.Height(100));

        if (GUILayout.Button("Execute"))
        {
            database.ExecuteSQLQuery();
        }
    }

    private void DrawPlaceholder()
    {
        EditorGUILayout.LabelField("Placeholder tab for future features.", EditorStyles.boldLabel);
    }

    private void AddNewConnection()
    {
        string path = EditorUtility.OpenFilePanel("Select SQLite Database", "", "sqlite");
        if (!string.IsNullOrEmpty(path))
        {
            connections.Add(new DatabaseConnection(path));
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Texture2D result = new Texture2D(width, height);
        Color[] pix = new Color[width * height];

        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col; // Preserve transparency
        }

        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}