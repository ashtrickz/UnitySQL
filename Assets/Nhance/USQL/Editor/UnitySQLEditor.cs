using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Linq;
using Mono.Data.Sqlite;
using Object = UnityEngine.Object;

public class UnitySQLManager : EditorWindow
{
    private List<DatabaseConnection> connections = new List<DatabaseConnection>();
    private int selectedConnectionIndex = -1;
    private int selectedDatabaseIndex = -1;
    private string[] tabs = {"Overview", "Structure", "Search", "SQL"};
    private int selectedTab = 0;
    private string selectedTable;
    private string newEntryData = "";

    private string newColumnName = "";
    private int selectedColumnTypeIndex = 0; // Stores the selected index
    private string selectedTableForColumns = "";

    private string[] availableColumnTypes =
        {"TEXT", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"}; // Available column types

    private readonly string[] availableOperators = {"=", "!=", "LIKE", "<", ">", "<=", ">="};

    private int currentPage = 0;
    private const int rowsPerPage = 5;

    private string selectedTableForContent = "";
    private Vector2 scrollPosition;

    private const string SaveKey = "UnitySQLManager_SaveData";

    private GenericMenu columnContextMenu;

    // SQL Tab

    private List<string[]> tableData = new List<string[]>(); // Stores query results
    private string[] columnNames = new string[0];
    private string sqlExecutionMessage = "";
    private Vector2 sqlScrollPosition;

    [MenuItem("Nhance/Tools/UnitySQL Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<UnitySQLManager>("Nhance Unity SQL Manager");
        window.LoadSessionData();
    }

    public void SaveSessionData()
    {
        SaveData saveData = new SaveData();

        // Store connection paths
        saveData.Connections = connections.Select(conn => conn.Path).ToList();

        // Store expanded states for connections
        saveData.ExpandedConnections = new List<KeyValuePairStringBool>();
        foreach (var pair in connectionStates)
        {
            saveData.ExpandedConnections.Add(new KeyValuePairStringBool {Key = pair.Key.Path, Value = pair.Value});
        }

        // Store expanded states for databases
        saveData.ExpandedDatabases = new List<KeyValuePairStringList>();
        foreach (var connection in connections)
        {
            if (connection.Databases.Count > 0)
            {
                List<string> expandedDbs = connection.Databases
                    .Where(db => databaseStates.ContainsKey(db) && databaseStates[db])
                    .Select(db => db.Name)
                    .ToList();

                saveData.ExpandedDatabases.Add(
                    new KeyValuePairStringList {Key = connection.Path, Value = expandedDbs});
            }
        }

        // Store opened tables
        saveData.OpenedTables = new List<KeyValuePairStringString>();
        foreach (var connection in connections)
        {
            if (saveData.OpenedTables.Any(entry => entry.Key == connection.Path)) continue;

            if (selectedConnectionIndex >= 0 && selectedDatabaseIndex >= 0 &&
                connections[selectedConnectionIndex] == connection)
            {
                if (!string.IsNullOrEmpty(selectedTableForContent))
                {
                    saveData.OpenedTables.Add(new KeyValuePairStringString
                        {Key = connection.Path, Value = selectedTableForContent});
                }
            }
        }

        // Convert to JSON and save in EditorPrefs
        string json = JsonUtility.ToJson(saveData, true);
        EditorPrefs.SetString(SaveKey, json);

        Debug.Log($"[DEBUG] Successfully Saved JSON:\n{json}");
    }

    private void LoadSessionData()
    {
        if (!EditorPrefs.HasKey(SaveKey)) return;

        string json = EditorPrefs.GetString(SaveKey);
        SaveData saveData = JsonUtility.FromJson<SaveData>(json);

        if (saveData?.Connections != null)
        {
            foreach (var path in saveData.Connections)
            {
                var connection = new DatabaseConnection(path);
                connections.Add(connection);
            }
        }

        // Restore expanded connections
        connectionStates.Clear();
        foreach (var pair in saveData.ExpandedConnections)
        {
            var connection = connections.FirstOrDefault(c => c.Path == pair.Key);
            if (connection != null)
            {
                connectionStates[connection] = pair.Value;
            }
        }

        // Restore expanded databases
        databaseStates.Clear();
        foreach (var pair in saveData.ExpandedDatabases)
        {
            var connection = connections.FirstOrDefault(c => c.Path == pair.Key);
            if (connection != null)
            {
                foreach (var db in connection.Databases)
                {
                    if (pair.Value.Contains(db.Name))
                    {
                        databaseStates[db] = true;
                    }
                }
            }
        }

        // Restore opened tables
        if (saveData.OpenedTables.Count > 0)
        {
            foreach (var pair in saveData.OpenedTables)
            {
                var connection = connections.FirstOrDefault(c => c.Path == pair.Key);
                if (connection != null && connection.Databases.Count > 0)
                {
                    selectedConnectionIndex = connections.IndexOf(connection);
                    selectedDatabaseIndex = 0;
                    selectedTableForContent = pair.Value;

                    var database = connection.Databases[selectedDatabaseIndex];
                    database.LoadTableContent(pair.Value);

                    Debug.Log($"[DEBUG] Restored Opened Table: {pair.Value} for {connection.Path}");
                }
            }
        }

        Repaint();
        Debug.Log("[DEBUG] Successfully Loaded Session Data");
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
            EditorGUILayout.BeginHorizontal();

            bool isConnectionExpanded = connectionStates[connection];
            string connectionArrow = isConnectionExpanded ? "▼" : "▶";

            if (GUILayout.Button($" {connectionArrow}\t {connection.Name} connection", EditorStyles.boldLabel))
            {
                connectionStates[connection] = !isConnectionExpanded;
                SaveSessionData(); // Save expanded state
            }

            // **"+" Button to Add Database**
            if (GUILayout.Button("➕", GUILayout.Width(25), GUILayout.Height(20)))
            {
                OpenCreateDatabaseWindow(connection);
            }

            // **"X" Button to Remove Connection**
            if (GUILayout.Button("❌", GUILayout.Width(25), GUILayout.Height(20)))
            {
                OpenDeleteConnectionWindow(connection);
            }

            EditorGUILayout.EndHorizontal();

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

                    // EditorGUILayout.BeginVertical();

                    if (GUILayout.Button($" {databaseArrow}", EditorStyles.boldLabel, GUILayout.Width(15)))
                    {
                        databaseStates[database] = !isDatabaseExpanded;
                        selectedDatabaseIndex = connection.Databases.IndexOf(database);
                        selectedConnectionIndex = i;
                        SaveSessionData();
                    }

                    if (GUILayout.Button($"\t{database.Name}", EditorStyles.boldLabel,
                            GUILayout.ExpandWidth(true)))
                    {
                        selectedTab = 0;
                        selectedDatabaseIndex = connection.Databases.IndexOf(database);
                        selectedConnectionIndex = i;
                        selectedTableForContent = null;
                        SaveSessionData();
                    }

                    // EditorGUILayout.EndVertical();

                    // **"+" Button to Add Table**
                    if (GUILayout.Button("➕", GUILayout.Width(25), GUILayout.Height(20)))
                    {
                        OpenCreateTableWindow(database);
                    }

                    // **"X" Button to Remove Database**
                    if (GUILayout.Button("❌", GUILayout.Width(25), GUILayout.Height(20)))
                    {
                        OpenDeleteDatabaseWindow(connection, database);
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
                                selectedTab = 0;
                                selectedTableForContent = table.Name;
                                database.LoadTableContent(selectedTableForContent);
                                SaveSessionData();
                            }

                            // **"X" Button to Remove Table**
                            if (GUILayout.Button("❌", GUILayout.Width(25), GUILayout.Height(20)))
                            {
                                OpenDeleteTableWindow(database, table.Name);
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
                if (!string.IsNullOrEmpty(selectedTableForContent))
                    DrawDatabaseOverview();
                else
                    DrawDatabaseTables();
                break;

            case 1:
                DrawStructure();
                break;
            case 2:
                DrawSearch();
                break;
            case 3:
                DrawSQLExecutor();
                break;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSearch()
    {
        if (string.IsNullOrEmpty(selectedTableForContent))
        {
            EditorGUILayout.LabelField("Select a table to search.", EditorStyles.boldLabel);
            return;
        }

        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        List<Database.TableColumn> columns = database.GetTableColumns(selectedTableForContent);
        if (columns == null || columns.Count == 0)
        {
            EditorGUILayout.LabelField($"No columns found in table '{selectedTableForContent}'.",
                EditorStyles.boldLabel);
            return;
        }
        
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Search in table: {selectedTableForContent}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        // Search Button
        if (GUILayout.Button("🔍 Search", GUILayout.Height(20)))
        {
            ExecuteSearchQuery(database);
        }
        EditorGUILayout.EndHorizontal();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        // Table Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Column", EditorStyles.boldLabel, GUILayout.Width(150));
        EditorGUILayout.LabelField("Operator", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField("Value", EditorStyles.boldLabel, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        // Dynamic search filters
        if (searchFilters == null || searchFilters.Count != columns.Count)
        {
            searchFilters = new List<SearchFilter>();
            foreach (var column in columns)
            {
                searchFilters.Add(new SearchFilter {Column = column.Name, Operator = "=", Value = ""});
            }
        }

        for (int i = 0; i < columns.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Column Name
            EditorGUILayout.LabelField(columns[i].Name, GUILayout.Width(150));

            // Operator Selection (Fixed!)
            int selectedOperatorIndex = Array.IndexOf(availableOperators, searchFilters[i].Operator);
            selectedOperatorIndex =
                EditorGUILayout.Popup(selectedOperatorIndex, availableOperators, GUILayout.Width(100));
            searchFilters[i].Operator = availableOperators[selectedOperatorIndex];

            // Value Input
            searchFilters[i].Value = EditorGUILayout.TextField(searchFilters[i].Value, GUILayout.Width(150));

            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();

        // Display search results
        if (tableData.Count > 0)
        {
            DrawQueryResults();
        }
    }

    private void ExecuteSearchQuery(Database database)
    {
        try
        {
            tableData.Clear(); // Clear previous results
            List<string> conditions = new List<string>();

            foreach (var filter in searchFilters)
            {
                if (!string.IsNullOrEmpty(filter.Value))
                {
                    conditions.Add($"{filter.Column} {filter.Operator} '{filter.Value}'");
                }
            }

            string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            string query = $"SELECT * FROM {selectedTableForContent} {whereClause};";

            using (var connection = new SqliteConnection($"Data Source={database.Path};Version=3;"))
            {
                connection.Open();
                using (var dbCommand = connection.CreateCommand())
                {
                    dbCommand.CommandText = query;
                    ReadTableResults(dbCommand);
                }
            }

            sqlExecutionMessage = "Search executed successfully.";
        }
        catch (Exception ex)
        {
            sqlExecutionMessage = "Error: " + ex.Message;
        }
    }


    private void DrawDatabaseOverview()
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        DrawTableContentUI(database); // Only show table content
    }

    private List<bool> tableSelections = new List<bool>();
    private bool selectAllTables = false;

    private void DrawDatabaseTables()
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        List<string> tables = database.GetTableNames();
        if (tableSelections.Count != tables.Count)
        {
            tableSelections = new List<bool>(new bool[tables.Count]);
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Bulk Actions Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        selectAllTables = EditorGUILayout.Toggle(selectAllTables, GUILayout.Width(20));

        if (GUILayout.Button("Select All", GUILayout.Width(100)))
        {
            for (int i = 0; i < tableSelections.Count; i++)
            {
                tableSelections[i] = selectAllTables;
            }
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("📄 View", GUILayout.Width(80)))
        {
            PerformBulkTableAction("View");
        }

        if (GUILayout.Button("🏗️ Structure", GUILayout.Width(100)))
        {
            PerformBulkTableAction("Structure");
        }

        if (GUILayout.Button("🔍 Search", GUILayout.Width(80)))
        {
            PerformBulkTableAction("Search");
        }

        if (GUILayout.Button("➕ Insert", GUILayout.Width(80)))
        {
            PerformBulkTableAction("Insert");
        }

        if (GUILayout.Button("🗑️ Clear", GUILayout.Width(80)))
        {
            PerformBulkTableAction("Clear");
        }

        if (GUILayout.Button("❌ Delete", GUILayout.Width(80)))
        {
            PerformBulkTableAction("Delete");
        }

        EditorGUILayout.EndHorizontal();

        // Table list with checkboxes
        foreach (var (table, index) in tables.Select((table, index) => (table, index)))
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            tableSelections[index] = EditorGUILayout.Toggle(tableSelections[index], GUILayout.Width(20));

            // Table Name
            if (GUILayout.Button(table, EditorStyles.label, GUILayout.Width(200)))
            {
                selectedTableForContent = table;
                database.LoadTableContent(table);
                SaveSessionData();
            }

            // Action Buttons for Each Table
            if (GUILayout.Button("📄 View", GUILayout.Width(80)))
            {
                ExecuteTableAction(table, "View");
            }

            if (GUILayout.Button("🏗️ Structure", GUILayout.Width(100)))
            {
                ExecuteTableAction(table, "Structure");
            }

            if (GUILayout.Button("🔍 Search", GUILayout.Width(80)))
            {
                ExecuteTableAction(table, "Search");
            }

            if (GUILayout.Button("➕ Insert", GUILayout.Width(80)))
            {
                ExecuteTableAction(table, "Insert");
            }

            if (GUILayout.Button("🗑️ Clear", GUILayout.Width(80)))
            {
                ExecuteTableAction(table, "Clear");
            }

            if (GUILayout.Button("❌ Delete", GUILayout.Width(80)))
            {
                ExecuteTableAction(table, "Delete");
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }


    private void OpenCreateTableWindow(Database database)
    {
        CreateTableWindow.ShowWindow(database);
    }

    private void DrawTableContentUI(Database database)
    {
        if (string.IsNullOrEmpty(selectedTableForContent)) return;

        var table = database.Tables.FirstOrDefault(t => t.Name == selectedTableForContent);
        if (table == null) return;

        string primaryKeyColumn = database.GetPrimaryKeyColumn(selectedTableForContent);
        int totalRows = table.Data.Count;
        int totalPages = Mathf.CeilToInt((float) totalRows / rowsPerPage);
        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(500));


        if (table.Data.Count > 0)
        {
            // **Column Headers with Right-Click Menu**
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            foreach (var column in table.Data[0].Keys)
            {
                string displayColumn = column == primaryKeyColumn ? $"🔑 {column}" : column;
                GUIStyle columnStyle = new GUIStyle(GUI.skin.label)
                    {alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold};

                Rect headerRect = GUILayoutUtility.GetRect(120, 25);
                GUI.Box(headerRect, displayColumn, columnStyle);

                // **Right-Click Context Menu for Column Headers**
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                    headerRect.Contains(Event.current.mousePosition))
                {
                    ShowColumnContextMenu(column, selectedTableForContent);
                    Event.current.Use();
                }
            }

            if (GUILayout.Button("➕", GUILayout.Width(25), GUILayout.Height(25)))
                OpenAddColumnWindow(selectedTableForContent);
            EditorGUILayout.EndHorizontal();

            // **Table Rows**
            // Paginated Table Rows
            for (int i = currentPage * rowsPerPage; i < Mathf.Min((currentPage + 1) * rowsPerPage, totalRows); i++)
            {
                var row = table.Data[i];

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                foreach (var column in row.Keys)
                {
                    object value = row[column];

                    Rect cellRect = GUILayoutUtility.GetRect(120, 25);
                    GUI.Box(cellRect, "", EditorStyles.helpBox);

                    // **Right-Click Context Menu for Cells**
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        ShowCellContextMenu(selectedTableForContent, row, column, ConvertValueToString(value));
                        Event.current.Use();
                    }

                    // **Fetch Column Type from Database**
                    string columnType = database.GetColumnType(selectedTableForContent, column);

                    // **Ensure PropertyField Always Renders for Unity Object Types**
                    if (columnType == "GameObject" || columnType == "Sprite")
                    {
                        EditorGUI.BeginChangeCheck();
                        Object newObject = EditorGUI.ObjectField(cellRect, value as UnityEngine.Object,
                            columnType == "GameObject" ? typeof(GameObject) : typeof(Sprite), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            UpdateCellValue(selectedTableForContent, row, column, newObject);
                        }
                    }
                    // **Standard Label for Text/Numeric Values**
                    else if (columnType == "Vector2" && value is Vector2 vector2)
                    {
                        GUI.Label(cellRect, $"Vector2({vector2.x}, {vector2.y})", EditorStyles.label);
                    }
                    else if (columnType == "Vector3" && value is Vector3 vector3)
                    {
                        GUI.Label(cellRect, $"Vector3({vector3.x}, {vector3.y}, {vector3.z})", EditorStyles.label);
                    }
                    else
                    {
                        string cellValue = value?.ToString() ?? ""; // Remove "NULL" label
                        GUI.Label(cellRect, cellValue,
                            new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter});
                    }
                }

                if (GUILayout.Button("...", GUILayout.Width(25), GUILayout.Height(25)))
                    ShowRowContextMenu(selectedTableForContent, row);
                EditorGUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("➕", GUILayout.Width(25), GUILayout.Height(25)))
                OpenAddRowWindow(selectedTableForContent);
        }
        else
        {
            EditorGUILayout.LabelField("No Data Found.", EditorStyles.boldLabel);
            if (GUILayout.Button("Add First Row", GUILayout.Height(25))) OpenAddRowWindow(selectedTableForContent);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("◀", GUILayout.Width(25)) && currentPage > 0)
        {
            currentPage--;
        }

        EditorGUILayout.LabelField($"Page {currentPage + 1} of {totalPages}", EditorStyles.boldLabel,
            GUILayout.Width(100));

        if (GUILayout.Button("▶", GUILayout.Width(25)) && currentPage < totalPages - 1)
        {
            currentPage++;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.EndScrollView();
    }

    private void ShowRowContextMenu(string tableName, Dictionary<string, object> rowData)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Duplicate Row"), false, () => OpenDuplicateRowWindow(tableName, rowData));
        menu.AddItem(new GUIContent("Delete Row"), false, () => OpenDeleteRowWindow(tableName, rowData));
        menu.ShowAsContext();
    }

    public void DuplicateRow(string tableName, Dictionary<string, object> newRowData)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            database.InsertRow(tableName, newRowData);
            database.LoadTableContent(tableName);
        }
    }


    private void OpenDuplicateRowWindow(string tableName, Dictionary<string, object> rowData)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            string primaryKeyColumn = database.GetPrimaryKeyColumn(tableName);
            DuplicateRowWindow.ShowWindow(this, tableName, rowData, primaryKeyColumn, database);
        }
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

    private string ConvertValueToString(object value)
    {
        if (value == null)
            return "";

        if (value is Vector2 vector2)
            return $"{vector2.x}, {vector2.y}";

        if (value is Vector3 vector3)
            return $"{vector3.x}, {vector3.y}, {vector3.z}";

        if (value is Sprite sprite)
            return sprite != null ? $"[Sprite] {sprite.name}" : "";

        if (value is GameObject gameObject)
            return gameObject != null ? $"[GameObject] {gameObject.name}" : "";

        return value.ToString();
    }


    public void RemoveConnection(DatabaseConnection connection)
    {
        connections.Remove(connection);
        SaveSessionData();
        Debug.Log($"[INFO] Connection '{connection.Name}' removed.");
    }

    public void RemoveDatabase(DatabaseConnection connection, Database database)
    {
        connection.Databases.Remove(database);
        SaveSessionData();
        Debug.Log($"[INFO] Database '{database.Name}' removed.");
    }

    private void OpenDeleteConnectionWindow(DatabaseConnection connection)
    {
        ConfirmationWindow.ShowWindow(
            "Delete Connection",
            $"Are you sure you want to delete connection '{connection.Name}'?",
            () => RemoveConnection(connection)
        );
    }

    private void OpenDeleteDatabaseWindow(DatabaseConnection connection, Database database)
    {
        ConfirmationWindow.ShowWindow(
            "Delete Database",
            $"Are you sure you want to delete database '{database.Name}'?",
            () => RemoveDatabase(connection, database)
        );
    }

    private void OpenDeleteTableWindow(Database database, string tableName)
    {
        ConfirmationWindow.ShowWindow(
            "Delete Table",
            $"Are you sure you want to delete table '{tableName}'?",
            () => database.DeleteTable(tableName)
        );
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

    private void OpenCreateDatabaseWindow(DatabaseConnection connection)
    {
        CreateDatabaseWindow.ShowWindow(this, connection);
    }

    private void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName,
        object newValue)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            string convertedValue = "";

            string columnType = database.GetColumnType(tableName, columnName);

            try
            {
                if (newValue == null)
                {
                    convertedValue = "NULL"; // Store as NULL in the database
                }
                else if (columnType == "GameObject" && newValue is GameObject gameObject)
                {
                    convertedValue = AssetDatabase.GetAssetPath(gameObject); // Store asset path
                }
                else if (columnType == "Sprite" && newValue is Sprite sprite)
                {
                    convertedValue = AssetDatabase.GetAssetPath(sprite); // Store asset path
                }
                else if (columnType == "Vector2" && newValue is Vector2 vector2)
                {
                    convertedValue = $"{vector2.x},{vector2.y}"; // Store as "x,y"
                }
                else if (columnType == "Vector3" && newValue is Vector3 vector3)
                {
                    convertedValue = $"{vector3.x},{vector3.y},{vector3.z}"; // Store as "x,y,z"
                }
                else
                {
                    convertedValue = newValue.ToString();
                }

                if (rowData.ContainsKey(columnName))
                {
                    rowData[columnName] = newValue;
                }

                database.UpdateCellValue(tableName, rowData, columnName, convertedValue);
                database.LoadTableContent(tableName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ERROR] Failed to update cell '{columnName}' in table '{tableName}': {e.Message}");
            }
        }
    }


    private void CopyCellToClipboard(string cellValue)
    {
        EditorGUIUtility.systemCopyBuffer = cellValue;
        Debug.Log($"[INFO] Copied value: {cellValue}");
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
        AddColumnWindow.ShowWindow(this, tableName);
    }

    private void ShowColumnContextMenu(string columnName, string tableName)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Edit Column"), false, () => OpenChangeColumnWindow(tableName, columnName));
        menu.AddItem(new GUIContent("Delete Column"), false, () => OpenDeleteColumnWindow(tableName, columnName));
        menu.AddItem(new GUIContent("Make Primary Key"), false, () => MakeColumnPrimaryKey(tableName, columnName));
        menu.ShowAsContext();
    }

    private void OpenChangeColumnWindow(string tableName, string columnName)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            ChangeColumnWindow.ShowWindow(database, tableName, columnName);
        }
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

    private void DrawSQLExecutor()
    {
        EditorGUILayout.LabelField("SQL Query Executor", EditorStyles.boldLabel);
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        database.SQLQuery = EditorGUILayout.TextArea(database.SQLQuery, GUILayout.Height(100));

        if (GUILayout.Button("Execute"))
        {
            ExecuteSQLQuery(database);
        }

        // Display execution messages (errors or success)
        if (!string.IsNullOrEmpty(sqlExecutionMessage))
        {
            EditorGUILayout.HelpBox(sqlExecutionMessage, MessageType.Info);
        }

        // Display table results
        if (tableData.Count > 0)
        {
            DrawQueryResults();
        }
    }

    private void ExecuteSQLQuery(Database database)
    {
        try
        {
            tableData.Clear(); // Clear previous results

            using (var connection = new SqliteConnection($"Data Source={database.Path};Version=3;"))
            {
                connection.Open();
                using (var dbCommand = connection.CreateCommand())
                {
                    dbCommand.CommandText = database.SQLQuery;

                    if (database.SQLQuery.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadTableResults(dbCommand);
                    }
                    else
                    {
                        int affectedRows = dbCommand.ExecuteNonQuery();
                        sqlExecutionMessage = $"Query executed successfully. Affected rows: {affectedRows}";
                    }
                }
            }

            DrawConnectionsPanel();
        }
        catch (Exception ex)
        {
            sqlExecutionMessage = "Error: " + ex.Message;
        }
    }


    private void ReadTableResults(IDbCommand dbCommand)
    {
        using (IDataReader reader = dbCommand.ExecuteReader())
        {
            int columnCount = reader.FieldCount;
            columnNames = new string[columnCount];

            for (int i = 0; i < columnCount; i++)
            {
                columnNames[i] = reader.GetName(i);
            }

            while (reader.Read())
            {
                string[] row = new string[columnCount];
                for (int i = 0; i < columnCount; i++)
                {
                    row[i] = reader.GetValue(i).ToString();
                }

                tableData.Add(row);
            }
        }

        sqlExecutionMessage = "Query executed successfully.";
    }

    private void DrawQueryResults()
    {
        GUILayout.Label("Query Results", EditorStyles.boldLabel);
        sqlScrollPosition = EditorGUILayout.BeginScrollView(sqlScrollPosition, GUILayout.Height(300));

        // Draw column headers
        if (columnNames.Length > 0)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            foreach (string col in columnNames)
            {
                GUILayout.Label(col, EditorStyles.boldLabel, GUILayout.Width(150));
            }

            EditorGUILayout.EndHorizontal();
        }

        // Draw table rows
        foreach (var row in tableData)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (var cell in row)
            {
                GUILayout.Label(cell, GUILayout.Width(150));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }


    private List<bool> columnSelections = new List<bool>();
    private bool selectAllColumns = false;

    private List<string> editedColumnNames = new List<string>();
    private List<int> selectedColumnTypeIndices = new List<int>();
    private List<bool> editedPrimaryKeys = new List<bool>();

    private void DrawStructure()
    {
        if (string.IsNullOrEmpty(selectedTableForContent))
        {
            EditorGUILayout.LabelField("Select a table to view its structure.", EditorStyles.boldLabel);
            return;
        }

        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        List<Database.TableColumn> columns = database.GetTableColumns(selectedTableForContent);
        if (columns == null || columns.Count == 0)
        {
            EditorGUILayout.LabelField($"No columns found in table '{selectedTableForContent}'.",
                EditorStyles.boldLabel);
            return;
        }

        // Initialize storage lists if not set
        if (editedColumnNames.Count != columns.Count || selectedColumnTypeIndices.Count != columns.Count)
        {
            editedColumnNames = columns.Select(col => col.Name).ToList();
            selectedColumnTypeIndices = columns.Select(col => Array.IndexOf(availableColumnTypes, col.Type)).ToList();
            editedPrimaryKeys = columns.Select(col => col.IsPrimaryKey).ToList();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // **Save Changes Button**
        // Ensure selection list is initialized correctly
        if (columnSelections.Count != columns.Count)
        {
            columnSelections = new List<bool>(new bool[columns.Count]);
        }

// Bulk Actions Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        selectAllColumns = EditorGUILayout.Toggle(selectAllColumns, GUILayout.Width(20));

        if (GUILayout.Button("Select All", GUILayout.Width(100)))
        {
            for (int i = 0; i < columnSelections.Count; i++)
            {
                columnSelections[i] = selectAllColumns;
            }
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("📄 View", GUILayout.Width(80)))
        {
            PerformBulkColumnAction("View");
        }

        if (GUILayout.Button("❌ Delete", GUILayout.Width(80)))
        {
            PerformBulkColumnAction("Delete");
        }

        if (GUILayout.Button("💾 Save Changes", GUILayout.Width(150)))
        {
            SaveColumnChanges(database);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // **Table Header**
// Table header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("✔", GUILayout.Width(20));
        EditorGUILayout.LabelField("Column Name", EditorStyles.boldLabel, GUILayout.Width(150));
        EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField("Primary Key", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

// Table rows (with checkboxes)
        for (int i = 0; i < columns.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            columnSelections[i] = EditorGUILayout.Toggle(columnSelections[i], GUILayout.Width(20));
            editedColumnNames[i] = EditorGUILayout.TextField(editedColumnNames[i], GUILayout.Width(150));
            selectedColumnTypeIndices[i] = EditorGUILayout.Popup(selectedColumnTypeIndices[i], availableColumnTypes,
                GUILayout.Width(100));

            bool wasPrimaryKey = editedPrimaryKeys[i];
            editedPrimaryKeys[i] = EditorGUILayout.Toggle(editedPrimaryKeys[i], GUILayout.Width(100));

            if (editedPrimaryKeys[i] && !wasPrimaryKey)
            {
                for (int j = 0; j < editedPrimaryKeys.Count; j++)
                {
                    if (j != i) editedPrimaryKeys[j] = false;
                }
            }

            if (GUILayout.Button("📄 View", GUILayout.Width(80)))
            {
                PerformBulkAction("View", columns[i].Name);
            }
            
            if (GUILayout.Button("❌ Delete", GUILayout.Width(80)))
            {
                PerformBulkAction("Delete", columns[i].Name);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("➕", GUILayout.Width(25), GUILayout.Height(20)))
        {
            OpenAddColumnWindow(selectedTableForContent);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void PerformBulkColumnAction(string action)
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        List<Database.TableColumn> columns = database.GetTableColumns(selectedTableForContent);
        List<string> selectedColumns = new List<string>();

        for (int i = 0; i < columns.Count; i++)
        {
            if (columnSelections[i])
            {
                selectedColumns.Add(columns[i].Name);
            }
        }

        if (selectedColumns.Count == 0) return;

        switch (action)
        {
            case "View":
                selectedTab = 3; // Switch to SQL Tab
                database.SQLQuery = $"SELECT {string.Join(", ", selectedColumns)} FROM {selectedTableForContent};";
                ExecuteSQLQuery(database);
                break;

            case "Delete":
                if (EditorUtility.DisplayDialog("Confirm Delete",
                        $"Are you sure you want to delete {selectedColumns.Count} selected columns?", "Yes", "No"))
                {
                    foreach (string columnName in selectedColumns)
                    {
                        database.DeleteColumn(selectedTableForContent, columnName);
                    }

                    database.LoadTableContent(selectedTableForContent);
                }

                break;
        }
    }


    private void SaveColumnChanges(Database database)
    {
        for (int i = 0; i < editedColumnNames.Count; i++)
        {
            string newColumnName = editedColumnNames[i];
            string newColumnType = availableColumnTypes[selectedColumnTypeIndices[i]];
            bool isPrimaryKey = editedPrimaryKeys[i] && !editedPrimaryKeys.Contains(true) || editedPrimaryKeys[i];

            // Apply Changes to the Database
            database.ModifyColumn(selectedTableForContent, i, newColumnName, newColumnType, isPrimaryKey);
        }

        database.LoadTableContent(selectedTableForContent);
        Debug.Log("Changes saved successfully!");
    }


    private void AddNewConnection()
    {
        string path = EditorUtility.OpenFilePanel("Select SQLite Database", "", "sqlite");
        if (!string.IsNullOrEmpty(path))
        {
            connections.Add(new DatabaseConnection(path));
            SaveSessionData(); // Save immediately after adding a new connection
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

    [System.Serializable]
    private class KeyValuePairStringBool
    {
        public string Key;
        public bool Value;
    }

    [System.Serializable]
    private class KeyValuePairStringList
    {
        public string Key;
        public List<string> Value;
    }

    [System.Serializable]
    private class KeyValuePairStringString
    {
        public string Key;
        public string Value;
    }

    private List<SearchFilter> searchFilters = new List<SearchFilter>();

    private class SearchFilter
    {
        public string Column;
        public string Operator;
        public string Value;
    }


    [System.Serializable]
    private class SaveData
    {
        public List<string> Connections = new List<string>(); // Stores connection paths

        public List<KeyValuePairStringBool>
            ExpandedConnections = new List<KeyValuePairStringBool>(); // Stores expanded state of connections

        public List<KeyValuePairStringList>
            ExpandedDatabases = new List<KeyValuePairStringList>(); // Stores expanded databases per connection

        public List<KeyValuePairStringString>
            OpenedTables = new List<KeyValuePairStringString>(); // Stores last selected table per connection
    }

    public bool DoesPrimaryKeyExist(string tableName, string primaryKeyColumn, string keyValue)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            return database.CheckPrimaryKeyExists(tableName, primaryKeyColumn, keyValue);
        }

        return false;
    }

    public void AddColumnToTable(string tableName, string columnName, string columnType)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            database.AddColumn(tableName, columnName, columnType);
            database.LoadTableContent(tableName);
        }
    }

    private void PerformBulkTableAction(string action)
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        List<string> tables = database.GetTableNames();
        for (int i = 0; i < tables.Count; i++)
        {
            if (tableSelections[i])
            {
                ExecuteTableAction(tables[i], action);
            }
        }
    }

    private void ExecuteTableAction(string tableName, string action)
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        switch (action)
        {
            case "View":
                selectedTab = 3; // Switch to SQL Tab
                database.SQLQuery = $"SELECT * FROM {tableName};";
                ExecuteSQLQuery(database);
                break;

            case "Structure":
                selectedTab = 1; // Switch to Structure Tab
                selectedTableForContent = tableName;
                break;

            case "Search":
                selectedTab = 2; // Switch to Search Tab
                selectedTableForContent = tableName;
                break;

            case "Insert":
                OpenAddRowWindow(tableName);
                break;

            case "Clear":
                if (EditorUtility.DisplayDialog("Confirm Clear", $"Are you sure you want to clear table '{tableName}'?",
                        "Yes", "No"))
                {
                    var table = database.Tables.First(t => t.Name == tableName);
                    table.ClearTable(database);
                }

                break;

            case "Delete":
                OpenDeleteTableWindow(database, tableName);
                break;
        }
    }


    private void PerformBulkAction(string action, string columnName)
    {
        var connection = connections[selectedConnectionIndex];
        var database = connection.Databases[selectedDatabaseIndex];

        switch (action)
        {
            case "View":
                selectedTab = 3; // Switch to SQL Tab
                    database.SQLQuery =
                        $"SELECT {columnName} FROM {selectedTableForContent};";
                    ExecuteSQLQuery(database);
                break;

            case "Edit":
                OpenChangeColumnWindow(selectedTableForContent, columnName);
                break;
            case "Delete":
                OpenDeleteColumnWindow(selectedTableForContent, columnName);
                break;
            case "PrimaryKey":
                database.MakePrimaryKey(selectedTableForContent, columnName);
                break;
        }
    }
}