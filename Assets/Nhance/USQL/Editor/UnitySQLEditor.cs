using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Linq;

public partial class UnitySQLManager : EditorWindow
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

    private const string SaveKey = "UnitySQLManager_SaveData";

    private GenericMenu columnContextMenu;

    [MenuItem("Nhance/Tools/UnitySQL Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<UnitySQLManager>("Nhance Unity SQL Manager");
        window.LoadSessionData();
    }

    private void SaveSessionData()
    {
        SaveData saveData = new SaveData();
        if (EditorPrefs.HasKey(SaveKey))
        {
            string existingJson = EditorPrefs.GetString(SaveKey);
            saveData = JsonUtility.FromJson<SaveData>(existingJson);
        }

        // Store connection paths
        saveData.Connections = connections.Select(conn => conn.Path).ToList();

        // Convert ExpandedConnections dictionary to list
        saveData.ExpandedConnections = new List<KeyValuePairStringBool>();
        foreach (var pair in connectionStates)
        {
            saveData.ExpandedConnections.Add(new KeyValuePairStringBool { Key = pair.Key.Path, Value = pair.Value });
        }

        // Convert ExpandedDatabases dictionary to list
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
                    new KeyValuePairStringList { Key = connection.Path, Value = expandedDbs });
            }
        }

        // Convert OpenedTables dictionary to list
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
                        { Key = connection.Path, Value = selectedTableForContent });
                }
            }
        }

        // Convert to JSON and save
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

        // Convert ExpandedConnections list back into dictionary
        connectionStates.Clear();
        foreach (var pair in saveData.ExpandedConnections)
        {
            var connection = connections.FirstOrDefault(c => c.Path == pair.Key);
            if (connection != null)
            {
                connectionStates[connection] = pair.Value;
            }
        }

        // Convert ExpandedDatabases list back into dictionary
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

        // Convert OpenedTables list back into dictionary and restore selected table
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

            bool isConnectionExpanded = connectionStates[connection];
            string connectionArrow = isConnectionExpanded ? "▼" : "▶";

            if (GUILayout.Button($" {connectionArrow}\t {connection.Name} connection", EditorStyles.boldLabel))
            {
                connectionStates[connection] = !isConnectionExpanded;
                SaveSessionData();
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
                        SaveSessionData();
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
                                SaveSessionData();
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
    if (string.IsNullOrEmpty(selectedTableForContent)) return;

    var table = database.Tables.FirstOrDefault(t => t.Name == selectedTableForContent);
    if (table == null) return;

    string primaryKeyColumn = database.GetPrimaryKeyColumn(selectedTableForContent);
    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

    if (table.Data.Count > 0)
    {
        // **Column Headers with Right-Click Menu**
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        foreach (var column in table.Data[0].Keys)
        {
            string displayColumn = column == primaryKeyColumn ? $"🔑 {column}" : column;
            GUIStyle columnStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            Rect headerRect = GUILayoutUtility.GetRect(120, 25);
            GUI.Box(headerRect, displayColumn, columnStyle);

            // **Right-Click Context Menu for Column Headers**
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && headerRect.Contains(Event.current.mousePosition))
            {
                ShowColumnContextMenu(column, selectedTableForContent);
                Event.current.Use();
            }
        }
        if (GUILayout.Button("+", GUILayout.Width(25), GUILayout.Height(25))) OpenAddColumnWindow(selectedTableForContent);
        EditorGUILayout.EndHorizontal();

        // **Table Rows**
        foreach (var row in table.Data)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            foreach (var column in row.Keys)
            {
                object value = row[column];

                Rect cellRect = GUILayoutUtility.GetRect(120, 25);
                GUI.Box(cellRect, "", EditorStyles.helpBox);

                // **Right-Click Context Menu for Cells**
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && cellRect.Contains(Event.current.mousePosition))
                {
                    ShowCellContextMenu(selectedTableForContent, row, column, ConvertValueToString(value));
                    Event.current.Use();
                }

                // **Ensure SerializedObject for Higher Types**
                SerializedObject serializedObject = new SerializedObject(this);
                SerializedProperty property = serializedObject.FindProperty(column);

                GUIStyle cellStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

                // **GameObject and Sprite Fields**
                if (column.Contains("GameObject") || column.Contains("Sprite") || column.Contains("higher"))
                {
                    EditorGUI.BeginChangeCheck();
                    Object newObject = EditorGUI.ObjectField(cellRect, (Object)value, column.Contains("GameObject") ? typeof(GameObject) : typeof(Sprite), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        UpdateCellValue(selectedTableForContent, row, column, newObject);
                    }
                }
                // **Standard Label for Text/Numeric Values**
                else
                {
                    string cellValue = value?.ToString() ?? "NULL";
                    GUI.Label(cellRect, cellValue, cellStyle);
                }
            }
            if (GUILayout.Button("...", GUILayout.Width(25), GUILayout.Height(25))) ShowRowContextMenu(selectedTableForContent, row);
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+", GUILayout.Width(25), GUILayout.Height(25))) OpenAddRowWindow(selectedTableForContent);
    }
    else
    {
        EditorGUILayout.LabelField("No Data Found.", EditorStyles.boldLabel);
        if (GUILayout.Button("Add First Row", GUILayout.Height(25))) OpenAddRowWindow(selectedTableForContent);
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
    
    private string ConvertValueToString(object value)
    {
        if (value == null)
            return ""; // Now shows an empty field instead of "NULL"

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

    private void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName, object newValue)
    {
        var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

        if (database != null)
        {
            string convertedValue = "";

            if (newValue == null)
            {
                convertedValue = ""; // Store as empty string in database
            }
            else if (newValue is Vector2 vector2)
            {
                convertedValue = $"{vector2.x},{vector2.y}";
            }
            else if (newValue is Vector3 vector3)
            {
                convertedValue = $"{vector3.x},{vector3.y},{vector3.z}";
            }
            else if (newValue is Sprite sprite)
            {
                convertedValue = AssetDatabase.GetAssetPath(sprite); // Store Sprite Asset Path
            }
            else if (newValue is GameObject gameObject)
            {
                convertedValue = AssetDatabase.GetAssetPath(gameObject); // Store GameObject Prefab Path
            }
            else
            {
                convertedValue = newValue.ToString();
            }

            database.UpdateCellValue(tableName, rowData, columnName, convertedValue);
            database.LoadTableContent(tableName);
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
}