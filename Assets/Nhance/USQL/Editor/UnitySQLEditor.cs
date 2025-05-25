using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Mono.Data.Sqlite;
using MySqlConnector;
using Nhance.USQL.AI;
using Nhance.USQL.Data;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Nhance.USQL.Editor
{
    public class UnitySqlManager : EditorWindow
    {
        #region Enums

        private enum EConnectionType
        {
            Local,
            Remote
        }

        private enum EsqlMode
        {
            Manual,
            AIPowered
        }

        #endregion

        #region Fields

        private EConnectionType selectedConnectionType = EConnectionType.Local;
        private EsqlMode currentMode = EsqlMode.Manual;

        private const int RowsPerPage = 10;
        private const string SaveKey = "UnitySQLManager_SaveData";

        private readonly string[] tabs = {"Overview", "Structure", "Search", "SQL"};

        private readonly string[] availableColumnTypes =
            {"TEXT", "VARCHAR", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"};

        private readonly string[] availableOperators = {"=", "!=", "LIKE", "<", ">", "<=", ">="};

        private int selectedConnectionIndex = -1;
        private int selectedDatabaseIndex = -1;
        private int selectedTab = 0;
        private int currentPage = 0;
        // private int selectedTableIndex = 0;
        private int selectedAIProviderIndex = 0;

        private string searchQuery = "";
        private string sqlExecutionMessage = "";
        private string selectedTableForContent = "";

        // Local connection
        private string localDatabasePath = "";

        // Remote connection
        private string remoteServerAddress = "127.0.0.1";
        private string remoteDatabaseName = "";
        private string remoteUserID = "";
        private string remotePassword = "";

        private string remoteSSLMode = "None";

        // AI
        private string aiPrompt = "";
        private string aiResponse = "";
        private string aiApiKey = "sk-...";

        private string systemPrompt =
            "You are a helpful SQL assistant. If the user provides database structure or actual table data, base your response directly on this information.  \n- If the user asks for a value (e.g., “what is the first name in testTable”), do not return SQL, but the actual value from the provided data.  \n- If the user asks for a query (e.g., “write a query to get all users”), then return SQL.  \nAlways respond briefly and clearly, and only with what was requested (either SQL or data).\n";

        private string[] columnNames = new string[0];

        private bool showAddConnectionForm = false;
        private bool selectAllTables = false;
        private bool selectAllColumns = false;
        private bool provideDatabaseData = false;
        private bool provideEntireDatabase = true;
        private bool showAISettings = false;

        private Vector2 scrollPosition;
        private Vector2 sqlScrollPosition;

        private List<int> selectedColumnTypeIndices = new List<int>();
        private List<bool> tableSelections = new List<bool>();
        private List<bool> editedPrimaryKeys = new List<bool>();
        private List<bool> columnSelections = new List<bool>();
        private List<string> tableNames = new List<string>();
        private List<string> selectedTableNames = new List<string>();
        private List<string> editedColumnNames = new List<string>();
        private List<string[]> tableData = new List<string[]>();
        private List<DatabaseConnection> connections = new List<DatabaseConnection>();
        private List<SearchFilter> searchFilters = new List<SearchFilter>();
        private List<IAIProvider> aiProviders;

        private Dictionary<DatabaseConnection, bool> connectionStates = new Dictionary<DatabaseConnection, bool>();
        private Dictionary<Database, bool> databaseStates = new Dictionary<Database, bool>();

        private IAIProvider aiProvider;

        #endregion

        private void OnEnable()
        {
            if (EditorPrefs.HasKey("UnitySQL_AIKey"))
                aiApiKey = EditorPrefs.GetString("UnitySQL_AIKey");

            InitializeAiProviders();
        }

        private void InitializeAiProviders()
        {
            aiProviders = new List<IAIProvider>
            {
                new OpenAIProvider(aiApiKey),
                new GroqAIProvider(aiApiKey),
                new DeepSeekAIProvider(aiApiKey),
                new NvidiaAIProvider(aiApiKey)
            };
        }

        [MenuItem("Nhance/Tools/UnitySQL Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnitySqlManager>("Nhance Unity SQL Manager");
            window.LoadSessionData();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Left Panel
            DrawConnectionsPanel();

            // Right Panel
            DrawWorkPanel();

            EditorGUILayout.EndHorizontal();
        }

        #region Left Panel

        private void DrawConnectionsPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));

            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);

            DrawSearchBar();

            DrawConnections();

            EditorGUILayout.Space();

            DrawAddConnectionButton();

            EditorGUILayout.EndVertical();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            searchQuery = EditorGUILayout.TextField(
                searchQuery, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
                searchQuery = "";

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddConnectionButton()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Add Connection", GUILayout.Width(150), GUILayout.Height(25)))
                showAddConnectionForm = !showAddConnectionForm;

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnections()
        {
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                var connectionMatches = string.IsNullOrEmpty(searchQuery) ||
                                        connection.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;

                var anyDatabaseMatches = connection.Databases.Any(db =>
                    string.IsNullOrEmpty(searchQuery) ||
                    db.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    db.Tables.Any(table => table.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                );

                if (!connectionMatches && !anyDatabaseMatches)
                    continue;

                if (!connectionStates.ContainsKey(connection))
                    connectionStates[connection] = false;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                var isConnectionExpanded = connectionStates[connection];
                var connectionArrow = isConnectionExpanded ? "▼" : "▶";

                if (GUILayout.Button($" {connectionArrow} {connection.Name} connection", EditorStyles.boldLabel))
                {
                    connectionStates[connection] = !isConnectionExpanded;
                    SaveSessionData();
                }

                if (GUILayout.Button("➕", EditorStyles.boldLabel, GUILayout.Width(25), GUILayout.Height(20)))
                    GenericModalWindow.Show(new CreateDatabaseContent(this, connection));

                if (GUILayout.Button("❌", EditorStyles.boldLabel, GUILayout.Width(25), GUILayout.Height(20)))
                    GenericModalWindow.Show(new ConfirmationContent(
                        $"Are you sure you want to delete connection '{connection.Name}'?",
                        () => RemoveConnection(connection)));

                EditorGUILayout.EndHorizontal();

                if (isConnectionExpanded)
                    DrawDatabases(connection, i);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawDatabases(DatabaseConnection connection, int i)
        {
            var consoleBackground = GetBackgroundTexture(new Color(.1f, .1f, .1f, 1));
            var containerStyle = GetHierarchyStyle(consoleBackground);

            foreach (var database in connection.Databases.Where(db =>
                string.IsNullOrEmpty(searchQuery) ||
                db.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                db.Tables.Any(table =>
                    table.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                if (!databaseStates.ContainsKey(database))
                    databaseStates[database] = false;

                var isDatabaseExpanded = databaseStates[database];
                var databaseArrow = isDatabaseExpanded ? "▼" : "▶";

                EditorGUILayout.BeginVertical(containerStyle);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);

                if (GUILayout.Button($" {databaseArrow}", EditorStyles.boldLabel, GUILayout.Width(15)))
                {
                    databaseStates[database] = !isDatabaseExpanded;
                    selectedDatabaseIndex = connection.Databases.IndexOf(database);
                    selectedConnectionIndex = i;
                    SaveSessionData();
                }

                if (GUILayout.Button($" {database.Name}", EditorStyles.boldLabel, GUILayout.ExpandWidth(true)))
                {
                    selectedTab = 0;
                    selectedDatabaseIndex = connection.Databases.IndexOf(database);
                    selectedConnectionIndex = i;
                    selectedTableForContent = null;
                    SaveSessionData();
                }

                if (GUILayout.Button("➕", EditorStyles.boldLabel, GUILayout.Width(25), GUILayout.Height(20)))
                    GenericModalWindow.Show(new CreateTableContent(database));

                if (GUILayout.Button("❌", EditorStyles.boldLabel, GUILayout.Width(25), GUILayout.Height(20)))
                    GenericModalWindow.Show(new ConfirmationContent(
                        $"Are you sure you want to delete database '{database.Name}'?",
                        () => RemoveDatabase(connection, database)));

                EditorGUILayout.EndHorizontal();

                if (isDatabaseExpanded)
                {
                    foreach (var table in database.Tables.Where(table =>
                        string.IsNullOrEmpty(searchQuery) ||
                        table.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        DrawTable(database, table);
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawTable(Database database, Table table)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.boldLabel, GUILayout.Height(2));

            GUILayout.Space(15);
            GUILayout.Button("", GUILayout.ExpandWidth(true), GUILayout.Height(2));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);

            if (GUILayout.Button($" • {table.Name}", EditorStyles.boldLabel))
            {
                selectedTab = 0;
                selectedTableForContent = table.Name;
                database.LoadTableContent(selectedTableForContent);
                SaveSessionData();

                if (GUILayout.Button("❌", EditorStyles.boldLabel, GUILayout.Width(25),
                    GUILayout.Height(20)))
                {
                    GenericModalWindow.Show(new ConfirmationContent(
                        $"Are you sure you want to delete table '{table.Name}'?",
                        () => database.DeleteTable(table.Name)));
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RemoveConnection(DatabaseConnection connection)
        {
            connections.Remove(connection);
            SaveSessionData();
            Debug.Log($"[INFO] Connection '{connection.Name}' removed.");
        }

        private void RemoveDatabase(DatabaseConnection connection, Database database)
        {
            connection.Databases.Remove(database);
            selectedConnectionIndex = -1;
            SaveSessionData();
            Debug.Log($"[INFO] Database '{database.Name}' removed.");
        }

        private Texture2D GetBackgroundTexture(Color color)
        {
            var backgroundTexture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            backgroundTexture.SetPixel(0, 0, color);
            backgroundTexture.Apply();

            return backgroundTexture;
        }

        private GUIStyle GetHierarchyStyle(Texture2D consoleBackground)
        {
            return new GUIStyle("box")
            {
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(5, 5, 5, 5),
                fontStyle = FontStyle.Normal,
                normal =
                {
                    background = consoleBackground,
                    textColor = Color.white
                }
            };
        }

        #endregion

        #region Right Panel

        private void DrawWorkPanel()
        {
            EditorGUILayout.BeginVertical(style: "box");

            if (showAddConnectionForm)
            {
                DrawConnectionForm();

                EditorGUILayout.EndVertical();
                return;
            }

            if (selectedConnectionIndex < 0 || selectedDatabaseIndex < 0)
            {
                EditorGUILayout.LabelField("Select a connection and database to manage.", GUILayout.Width(400));

                EditorGUILayout.EndVertical();
                return;
            }

            selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(0), GUILayout.Width(0));

            EditorGUILayout.BeginHorizontal();

            for (var index = 0; index < tabs.Length; index++)
            {
                var tab = tabs[index];
                var backColor = GUI.backgroundColor;
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white
                    }
                };

                if (selectedTab == index) GUI.backgroundColor = Color.cyan;

                if (GUILayout.Button(tab, style, GUILayout.Width(200), GUILayout.Height(25)))
                    selectedTab = index;

                GUI.backgroundColor = backColor;

                if (index == tabs.Length - 1) continue;
                GUILayout.Space(10);
            }

            EditorGUILayout.EndHorizontal();

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
                    DrawSqlExecutor();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        // Add connection tab
        private void DrawConnectionForm()
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            selectedConnectionType =
                (EConnectionType) EditorGUILayout.EnumPopup("Connection Type", selectedConnectionType);

            switch (selectedConnectionType)
            {
                case EConnectionType.Local:
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Database File", GUILayout.Width(100));
                    localDatabasePath = EditorGUILayout.TextField(localDatabasePath, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Browse", GUILayout.Width(70)))
                    {
                        var path = EditorUtility.OpenFilePanel("Select SQLite Database", "", "sqlite");
                        if (!string.IsNullOrEmpty(path))
                        {
                            localDatabasePath = path;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    break;
                }
                case EConnectionType.Remote:
                    EditorGUILayout.BeginVertical(GUILayout.Width(350));

                    remoteServerAddress = EditorGUILayout.TextField("Server Address", remoteServerAddress);
                    remoteDatabaseName = EditorGUILayout.TextField("Database Name", remoteDatabaseName);
                    remoteUserID = EditorGUILayout.TextField("User ID", remoteUserID);
                    remotePassword = EditorGUILayout.PasswordField("Password", remotePassword);
                    remoteSSLMode = EditorGUILayout.TextField("SSL Mode", remoteSSLMode);

                    EditorGUILayout.EndVertical();
                    break;
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Add", GUILayout.Height(25)))
            {
                if (selectedConnectionType == EConnectionType.Local)
                {
                    if (!string.IsNullOrEmpty(localDatabasePath))
                    {
                        connections.Add(
                            new DatabaseConnection(localDatabasePath, DatabaseConnection.EConnectionType.SQLite));
                        SaveSessionData();
                    }
                }
                else
                {
                    var connectionString =
                        $"Server={remoteServerAddress};" +
                        $"Database={remoteDatabaseName};" +
                        $"User ID={remoteUserID};" +
                        $"Password={remotePassword};" +
                        $"SslMode={remoteSSLMode};";
                    connections.Add(new DatabaseConnection(connectionString, DatabaseConnection.EConnectionType.MySQL));
                    SaveSessionData();
                }

                showAddConnectionForm = false;
            }

            EditorGUILayout.EndVertical();
        }

        #region Overview Tab

        private void DrawDatabaseOverview()
        {
            if (connections.Count <= selectedConnectionIndex) return;
            var connection = connections[selectedConnectionIndex];

            if (connection.Databases.Count <= selectedDatabaseIndex) return;
            var database = connection.Databases[selectedDatabaseIndex];

            DrawTableContentUI(database);
        }

        private void DrawTableContentUI(Database database)
        {
            if (string.IsNullOrEmpty(selectedTableForContent)) return;

            var table = database.Tables.FirstOrDefault(t => t.Name == selectedTableForContent);
            if (table == null) return;

            var primaryKeyColumn = database.GetPrimaryKeyColumn(selectedTableForContent);
            var totalRows = table.Data.Count;
            var totalPages = Mathf.CeilToInt((float) totalRows / RowsPerPage);
            currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(500));

            if (table.Data.Count > 0)
            {
                // **Column Headers with Right-Click Menu**
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                foreach (var column in table.Data[0].Keys)
                {
                    var displayColumn = column == primaryKeyColumn ? $"🔑 {column}" : column;
                    var columnStyle = new GUIStyle(GUI.skin.label)
                        {alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold};

                    var headerRect = GUILayoutUtility.GetRect(120, 25);
                    GUI.Box(headerRect, displayColumn, columnStyle);

                    // **Right-Click Context Menu for Column Headers**
                    if (Event.current.type != EventType.MouseDown || Event.current.button != 1 ||
                        !headerRect.Contains(Event.current.mousePosition)) continue;
                    ShowColumnContextMenu(column, selectedTableForContent);
                    Event.current.Use();
                }

                if (GUILayout.Button("➕", EditorStyles.boldLabel, GUILayout.Width(25), GUILayout.Height(25)))
                    GenericModalWindow.Show(new AddColumnContent(database, selectedTableForContent));
                EditorGUILayout.EndHorizontal();

                // **Table Rows**
                // Paginated Table Rows
                for (int i = currentPage * RowsPerPage; i < Mathf.Min((currentPage + 1) * RowsPerPage, totalRows); i++)
                {
                    var row = table.Data[i];

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    foreach (var column in row.Keys)
                    {
                        var value = row[column];

                        var cellStyle = EditorStyles.boldLabel;
                        cellStyle.alignment = TextAnchor.MiddleCenter;
                        cellStyle.fontStyle = FontStyle.Normal;
                        var cellRect = GUILayoutUtility.GetRect(120, 25);
                        GUI.Box(cellRect, "", cellStyle);

                        // **Right-Click Context Menu for Cells**
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                            cellRect.Contains(Event.current.mousePosition))
                        {
                            ShowCellContextMenu(selectedTableForContent, row, column, ConvertValueToString(value));
                            Event.current.Use();
                        }

                        // **Fetch Column Type from Database**
                        var columnType = database.GetColumnType(selectedTableForContent, column);

                        switch (columnType)
                        {
                            // **Ensure PropertyField Always Renders for Unity Object Types**
                            case "GameObject":
                            case "Sprite":
                            {
                                EditorGUI.BeginChangeCheck();
                                var newObject = EditorGUI.ObjectField(cellRect, value as UnityEngine.Object,
                                    columnType == "GameObject" ? typeof(GameObject) : typeof(Sprite), false);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    UpdateCellValue(selectedTableForContent, row, column, newObject);
                                }

                                break;
                            }
                            // **Standard Label for Text/Numeric Values**
                            case "Vector2" when value is Vector2 vector2:
                                GUI.Label(cellRect, $"Vector2({vector2.x}, {vector2.y})", cellStyle);
                                break;
                            case "Vector3" when value is Vector3 vector3:
                                GUI.Label(cellRect, $"Vector3({vector3.x}, {vector3.y}, {vector3.z})", cellStyle);
                                break;
                            default:
                            {
                                var cellValue = value?.ToString() ?? ""; // Remove "NULL" label
                                GUI.Label(cellRect, cellValue,
                                    new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter});
                                break;
                            }
                        }

                        cellStyle.alignment = TextAnchor.MiddleLeft;
                    }

                    if (GUILayout.Button("...", GUILayout.Width(25), GUILayout.Height(25)))
                        ShowRowContextMenu(selectedTableForContent, row);
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }

                var style = new GUIStyle(GUI.skin.button)
                {
                    normal =
                    {
                        textColor = Color.green
                    }
                };

                if (GUILayout.Button("Add Row", style, GUILayout.Width(125), GUILayout.Height(25)))
                    GenericModalWindow.Show(new AddRowContent(database, selectedTableForContent));
            }
            else
            {
                EditorGUILayout.LabelField("No Data Found.", EditorStyles.boldLabel);
                if (GUILayout.Button("Add First Row", GUILayout.Height(25)))
                    GenericModalWindow.Show(new AddRowContent(database, selectedTableForContent));
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("◀", EditorStyles.boldLabel, GUILayout.Width(25)) && currentPage > 0)
                currentPage--;

            EditorGUILayout.LabelField($"Page {currentPage + 1} of {totalPages}", EditorStyles.boldLabel,
                GUILayout.Width(100));

            if (GUILayout.Button("▶", EditorStyles.boldLabel, GUILayout.Width(25)) && currentPage < totalPages - 1)
                currentPage++;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.EndScrollView();
        }

        private void DrawDatabaseTables()
        {
            var connection = connections[selectedConnectionIndex];
            var database = connection.Databases[selectedDatabaseIndex];

            var tables = database.GetTableNames();
            if (tableSelections.Count != tables.Count)
            {
                tableSelections = new List<bool>(new bool[tables.Count]);
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Bulk Actions Header
            EditorGUILayout.BeginHorizontal(EditorStyles.boldLabel);
            selectAllTables = EditorGUILayout.Toggle(selectAllTables, GUILayout.Width(20));

            var headerStyle = EditorStyles.toolbarButton;
            headerStyle.normal.textColor = Color.white;

            if (GUILayout.Button("Select All", headerStyle, GUILayout.Width(100), GUILayout.Height(25)))
            {
                for (int i = 0; i < tableSelections.Count; i++)
                {
                    tableSelections[i] = selectAllTables;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📄 View", headerStyle, GUILayout.Width(80), GUILayout.Height(25)))
                PerformBulkTableAction("View");

            headerStyle.normal.textColor = new Color(184, 0, 231, 1);
            if (GUILayout.Button("🏗️ Structure", headerStyle, GUILayout.Width(100), GUILayout.Height(25)))
                PerformBulkTableAction("Structure");

            headerStyle.normal.textColor = Color.cyan;
            if (GUILayout.Button("🔍 Search", headerStyle, GUILayout.Width(80), GUILayout.Height(25)))
                PerformBulkTableAction("Search");

            headerStyle.normal.textColor = Color.green;
            if (GUILayout.Button("➕ Insert", headerStyle, GUILayout.Width(80), GUILayout.Height(25)))
                PerformBulkTableAction("Insert");

            headerStyle.normal.textColor = Color.yellow;
            if (GUILayout.Button("🗑️ Clear", headerStyle, GUILayout.Width(80), GUILayout.Height(25)))
                PerformBulkTableAction("Clear");

            headerStyle.normal.textColor = Color.red;
            if (GUILayout.Button("❌ Delete", headerStyle, GUILayout.Width(80), GUILayout.Height(25)))
                PerformBulkTableAction("Delete");

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

                var style = EditorStyles.toolbarButton;
                style.normal.textColor = Color.white;

                // Action Buttons for Each Table
                if (GUILayout.Button("📄 View", style, GUILayout.Width(80)))
                    ExecuteTableAction(table, "View");


                GUILayout.Space(5);
                style.normal.textColor = new Color(184, 0, 231, 1);

                if (GUILayout.Button("🏗️ Structure", style, GUILayout.Width(100)))
                    ExecuteTableAction(table, "Structure");


                GUILayout.Space(5);
                style.normal.textColor = Color.cyan;

                if (GUILayout.Button("🔍 Search", style, GUILayout.Width(80)))
                    ExecuteTableAction(table, "Search");

                GUILayout.Space(5);
                style.normal.textColor = Color.green;

                if (GUILayout.Button("➕ Insert", style, GUILayout.Width(80)))
                    ExecuteTableAction(table, "Insert");

                GUILayout.Space(5);
                style.normal.textColor = Color.yellow;

                if (GUILayout.Button("🗑️ Clear", style, GUILayout.Width(80)))
                    ExecuteTableAction(table, "Clear");

                GUILayout.Space(5);
                style.normal.textColor = Color.red;

                if (GUILayout.Button("❌ Delete", style, GUILayout.Width(80)))
                    ExecuteTableAction(table, "Delete");

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        #region Context Menus

        private void ShowRowContextMenu(string tableName, Dictionary<string, object> rowData)
        {
            var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
            if (database == null) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate Row"), false, () =>
                GenericModalWindow.Show(new DuplicateRowContent(database, tableName, rowData)));
            menu.AddItem(new GUIContent("Delete Row"), false, () =>
                GenericModalWindow.Show(new DeleteRowConfirmationContent(database, tableName, rowData)));
            menu.ShowAsContext();
        }

        private void ShowCellContextMenu(string tableName, Dictionary<string, object> rowData, string columnName,
            string cellValue)
        {
            var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
            if (database == null) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Change Value"), false, () =>
                GenericModalWindow.Show(new ChangeValueContent(database, tableName, rowData, columnName)));
            menu.AddItem(new GUIContent("Copy"), false, () =>
                EditorGUIUtility.systemCopyBuffer = cellValue);
            menu.ShowAsContext();
        }

        private void ShowColumnContextMenu(string columnName, string tableName)
        {
            var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];
            if (database == null) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Edit Column"), false, () =>
                GenericModalWindow.Show(new ChangeColumnContent(database, tableName, columnName)));
            menu.AddItem(new GUIContent("Delete Column"), false, () =>
                GenericModalWindow.Show(new DeleteColumnConfirmationContent(database, tableName, columnName)));
            menu.AddItem(new GUIContent("Make Primary Key"), false, () =>
            {
                database.MakePrimaryKeyColumn(tableName, columnName);
                database.LoadTableContent(tableName);
            });
            menu.ShowAsContext();
        }

        #endregion
        
        private string ConvertValueToString(object value)
        {
            return value switch
            {
                null => "",
                Vector2 vector2 => $"{vector2.x}, {vector2.y}",
                Vector3 vector3 => $"{vector3.x}, {vector3.y}, {vector3.z}",
                Sprite sprite => sprite != null ? $"[Sprite] {sprite.name}" : "",
                GameObject gameObject => gameObject != null ? $"[GameObject] {gameObject.name}" : "",
                _ => value.ToString()
            };
        }
                
        private void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName,
            object newValue)
        {
            var database = connections[selectedConnectionIndex].Databases[selectedDatabaseIndex];

            if (database == null) return;

            var columnType = database.GetColumnType(tableName, columnName);
            try
            {
                var convertedValue = "";
                if (newValue == null)
                {
                    convertedValue = "NULL"; // Store as NULL in the database
                }
                else
                    convertedValue = columnType switch
                    {
                        "GameObject" when newValue is GameObject gameObject => AssetDatabase.GetAssetPath(gameObject),
                        "Sprite" when newValue is Sprite sprite => AssetDatabase.GetAssetPath(sprite),
                        "Vector2" when newValue is Vector2 vector2 => $"{vector2.x},{vector2.y}",
                        "Vector3" when newValue is Vector3 vector3 => $"{vector3.x},{vector3.y},{vector3.z}",
                        _ => newValue.ToString()
                    };

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
        
        private void ExecuteTableAction(string tableName, string action)
        {
            var connection = connections[selectedConnectionIndex];
            var database = connection.Databases[selectedDatabaseIndex];

            switch (action)
            {
                case "View":
                    selectedTab = 3; // Switch to SQL Tab
                    database.SQLQuery = $"SELECT * FROM {tableName};";
                    ExecuteSqlQuery(database);
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
                    GenericModalWindow.Show(new AddRowContent(database, tableName));
                    break;

                case "Clear":
                    if (EditorUtility.DisplayDialog("Confirm Clear",
                        $"Are you sure you want to clear table '{tableName}'?",
                        "Yes", "No"))
                    {
                        var table = database.Tables.First(t => t.Name == tableName);
                        table.ClearTable(database);
                    }

                    break;

                case "Delete":
                    GenericModalWindow.Show(new ConfirmationContent(
                        $"Are you sure you want to delete table '{tableName}'?",
                        () => database.DeleteTable(tableName)));
                    break;
            }
        }

        #endregion

        #region Structure Tab

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
                selectedColumnTypeIndices =
                    columns.Select(col => Array.IndexOf(availableColumnTypes, col.Type)).ToList();
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

            var style = EditorStyles.toolbarButton;
            style.normal.textColor = Color.white;

            if (GUILayout.Button("Select All", style, GUILayout.Width(100)))
            {
                for (int i = 0; i < columnSelections.Count; i++)
                {
                    columnSelections[i] = selectAllColumns;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📄 View", style, GUILayout.Width(80)))
            {
                PerformBulkColumnAction("View");
            }

            style.normal.textColor = Color.red;
            if (GUILayout.Button("❌ Delete", style, GUILayout.Width(80)))
            {
                PerformBulkColumnAction("Delete");
            }

            style.normal.textColor = Color.white;
            if (GUILayout.Button("💾 Save Changes", style, GUILayout.Width(150)))
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

                style.normal.textColor = Color.white;
                if (GUILayout.Button("📄 View", style, GUILayout.Width(80)))
                {
                    PerformBulkAction("View", columns[i].Name);
                }

                style.normal.textColor = Color.red;
                if (GUILayout.Button("❌ Delete", style, GUILayout.Width(80)))
                {
                    PerformBulkAction("Delete", columns[i].Name);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            style.normal.textColor = Color.green;
            if (GUILayout.Button("➕", style, GUILayout.Width(25), GUILayout.Height(20)))
            {
                GenericModalWindow.Show(new AddColumnContent(database, selectedTableForContent));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        #region Bulk Actions

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
                    ExecuteSqlQuery(database);
                    break;

                case "Edit":
                    GenericModalWindow.Show(new ChangeColumnContent(database, selectedTableForContent, columnName));
                    break;
                case "Delete":
                    GenericModalWindow.Show(
                        new DeleteColumnConfirmationContent(database, selectedTableForContent, columnName));
                    break;
                case "PrimaryKey":
                    database.MakePrimaryKeyColumn(selectedTableForContent, columnName);
                    break;
            }
        }

        private void PerformBulkTableAction(string action)
        {
            var connection = connections[selectedConnectionIndex];
            var database = connection.Databases[selectedDatabaseIndex];

            var tables = database.GetTableNames();
            for (var i = 0; i < tables.Count; i++)
            {
                if (tableSelections[i])
                {
                    ExecuteTableAction(tables[i], action);
                }
            }
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
                    ExecuteSqlQuery(database);
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

        #endregion

        private void SaveColumnChanges(Database database)
        {
            for (var i = 0; i < editedColumnNames.Count; i++)
            {
                var newColumnName = editedColumnNames[i];
                var newColumnType = availableColumnTypes[selectedColumnTypeIndices[i]];
                var isPrimaryKey = editedPrimaryKeys[i] && !editedPrimaryKeys.Contains(true) || editedPrimaryKeys[i];

                database.ModifyColumn(selectedTableForContent, i, newColumnName, newColumnType, isPrimaryKey);
            }

            database.LoadTableContent(selectedTableForContent);
        }
        
        #endregion

        #region Search Tab

        private void DrawSearch()
        {
            if (string.IsNullOrEmpty(selectedTableForContent))
            {
                EditorGUILayout.LabelField("Select a table to search.", EditorStyles.boldLabel);
                return;
            }

            var connection = connections[selectedConnectionIndex];
            var database = connection.Databases[selectedDatabaseIndex];

            var columns = database.GetTableColumns(selectedTableForContent);

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
                ExecuteSearchQuery(database);

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
                var selectedOperatorIndex = Array.IndexOf(availableOperators, searchFilters[i].Operator);
                selectedOperatorIndex =
                    EditorGUILayout.Popup(selectedOperatorIndex, availableOperators, GUILayout.Width(100));
                searchFilters[i].Operator = availableOperators[selectedOperatorIndex];

                // Value Input
                searchFilters[i].Value = EditorGUILayout.TextField(searchFilters[i].Value, GUILayout.Width(150));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (tableData.Count > 0)
                DrawQueryResults();
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

                switch (database.ConnectionType)
                {
                    case DatabaseConnection.EConnectionType.SQLite:
                        using (var connection =
                            new SqliteConnection($"Data Source={database.ConnectionString};Version=3;"))
                        {
                            connection.Open();
                            using (var dbCommand = connection.CreateCommand())
                            {
                                dbCommand.CommandText = query;
                                ReadTableResults(dbCommand);
                            }
                        }

                        break;
                    case DatabaseConnection.EConnectionType.MySQL:
                        using (var connection = new MySqlConnection(database.ConnectionString))
                        {
                            connection.Open();
                            using (var dbCommand = connection.CreateCommand())
                            {
                                dbCommand.CommandText = query;
                                ReadTableResults(dbCommand);
                            }
                        }

                        break;
                }

                sqlExecutionMessage = "Search executed successfully.";
            }
            catch (Exception ex)
            {
                sqlExecutionMessage = "Error: " + ex.Message;
            }
        }

        #endregion

        #region Executor Tab
        
        private void DrawSqlExecutor()
        {
            EditorGUILayout.LabelField("SQL Query Executor", EditorStyles.boldLabel);

            DrawSQLExecutor_ModeSelection();

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            // Left side: Mode-specific UI
            EditorGUILayout.BeginVertical();
            switch (currentMode)
            {
                case EsqlMode.Manual:
                    DrawSqlExecutorManualMode();
                    break;
                case EsqlMode.AIPowered:

                    DrawSqlExecutorAISettings();

                    GUILayout.Space(10);

                    DrawSqlExecutorAIPrompt();
                    break;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSQLExecutor_ModeSelection()
        {
            var searchModes = new[] {"Manual", "AI Powered"};
            currentMode =
                (EsqlMode) GUILayout.Toolbar((int) currentMode, searchModes, GUILayout.Width(0), GUILayout.Height(0));

            EditorGUILayout.BeginHorizontal();

            for (var index = 0; index < searchModes.Length; index++)
            {
                var tab = searchModes[index];
                var backColor = GUI.backgroundColor;
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white
                    }
                };

                if ((int) currentMode == index) GUI.backgroundColor = Color.cyan;

                if (GUILayout.Button(tab, style, GUILayout.Width(200), GUILayout.Height(25)))
                    currentMode = (EsqlMode) index;

                GUI.backgroundColor = backColor;

                if (index == tabs.Length - 1) continue;
                GUILayout.Space(10);
            }

            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSqlExecutorManualMode()
        {
            var connection = connections[selectedConnectionIndex];
            var database = connection.Databases[selectedDatabaseIndex];

            database.SQLQuery = EditorGUILayout.TextArea(database.SQLQuery, GUILayout.Height(100));

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Execute", GUILayout.Width(200), GUILayout.Height(25)))
                ExecuteSqlQuery(database);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (!string.IsNullOrEmpty(sqlExecutionMessage))
                EditorGUILayout.HelpBox(sqlExecutionMessage, MessageType.Info);

            if (tableData.Count > 0)
                DrawQueryResults();
        }
        
        private void DrawSqlExecutorAISettings()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(500));

            showAISettings = EditorGUILayout.Foldout(showAISettings, "⚙ AI Settings", true,
                new GUIStyle(EditorStyles.foldout) {fontStyle = FontStyle.Bold});

            if (showAISettings)
            {
                EditorGUILayout.BeginVertical("box");
                DrawAISettingsPanel();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAISettingsPanel()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            selectedAIProviderIndex = EditorGUILayout.Popup("AI Provider", selectedAIProviderIndex,
                aiProviders.Select(p => p.Name).ToArray());
            aiProvider = aiProviders[selectedAIProviderIndex];

            provideDatabaseData = EditorGUILayout.Toggle("Provide Database Data", provideDatabaseData);

            if (provideDatabaseData)
            {
                provideEntireDatabase = EditorGUILayout.Toggle("Provide Entire Database", provideEntireDatabase);

                var connection = connections[selectedConnectionIndex];
                var database = connection.Databases[selectedDatabaseIndex];

                if (!provideEntireDatabase)
                {
                    if (tableNames.Count == 0)
                        tableNames = GetTableNames(database);

                    EditorGUILayout.LabelField("Select Tables to Send:", EditorStyles.boldLabel);

                    foreach (var t in tableNames)
                    {
                        var selected = selectedTableNames.Contains(t);
                        var toggle = EditorGUILayout.ToggleLeft(t, selected);

                        switch (toggle)
                        {
                            case true when !selected:
                                selectedTableNames.Add(t);
                                break;
                            case false when selected:
                                selectedTableNames.Remove(t);
                                break;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "Providing Entire Database can cost sufficient Token Waste so proceed with understanding of your actions!",
                        EditorStyles.helpBox);
                }
            }

            var newKey = EditorGUILayout.PasswordField("AI API Key", aiApiKey);
            if (newKey == aiApiKey) return;

            aiApiKey = newKey;
            EditorPrefs.SetString("UnitySQL_AIKey", aiApiKey);
        }

        private void DrawSqlExecutorAIPrompt()
        {
            EditorGUILayout.LabelField("Ask AI to generate or explain SQL", EditorStyles.boldLabel);
            aiPrompt = EditorGUILayout.TextField("Prompt", aiPrompt, GUILayout.Width(600), GUILayout.Height(100));

            GUILayout.Space(5);
            if (GUILayout.Button("Send to AI", GUILayout.Width(150), GUILayout.Height(25)))
            {
                var dataContext = "";

                var connection = connections[selectedConnectionIndex];
                var database = connection.Databases[selectedDatabaseIndex];

                if (provideDatabaseData)
                {
                    dataContext = provideEntireDatabase
                        ? GetDatabaseDataAsJson(database)
                        : GetMultipleTablesDataAsJson(database, selectedTableNames);
                }

                var fullSystemPrompt = systemPrompt;

                if (!string.IsNullOrEmpty(dataContext))
                    fullSystemPrompt += "\n\nHere is the provided data:\n" + dataContext;

                aiResponse = "Thinking...";
                aiProvider.SendPrompt(aiPrompt, fullSystemPrompt, (response) =>
                {
                    aiResponse = response;

                    var extracted = ExtractSqlFromMarkdown(response);
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        var sharedConnection = connections[selectedConnectionIndex];
                        var sharedDatabase = sharedConnection.Databases[selectedDatabaseIndex];
                        sharedDatabase.SQLQuery = extracted.Trim();
                        currentMode = EsqlMode.Manual;
                    }

                    Repaint();
                });
            }

            GUILayout.Space(5);
            if (string.IsNullOrEmpty(aiResponse)) return;

            EditorGUILayout.LabelField("AI Response:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(aiResponse, GUILayout.Height(100));
        }

        private string ExtractSqlFromMarkdown(string response)
        {
            const string startTag = "```sql";
            const string endTag = "```";

            var start = response.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (start == -1) return null;

            start += startTag.Length;
            var end = response.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
            return end == -1 ? null : response.Substring(start, end - start).Trim();
        }
        
        private void ExecuteSqlQuery(Database database)
        {
            try
            {
                tableData.Clear(); // Clear previous results

                using (var connection = new MySqlConnection(database.ConnectionString))
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
                            var affectedRows = dbCommand.ExecuteNonQuery();
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
                var columnCount = reader.FieldCount;
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
                var colStyle = new GUIStyle(EditorStyles.boldLabel);
                colStyle.fontSize = 14;
                foreach (string col in columnNames)
                {
                    GUILayout.Label(col, colStyle, GUILayout.Width(150), GUILayout.Height(25));
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
        
        #endregion

        #endregion
        
        #region WindowData Handling

        public void SaveSessionData()
        {
            var saveData = new SaveData
            {
                Connections = connections.Select(conn => conn.Path).ToList(),
                ExpandedConnections = new List<KeyValuePairStringBool>()
            };

            foreach (var pair in connectionStates)
            {
                saveData.ExpandedConnections.Add(new KeyValuePairStringBool {Key = pair.Key.Path, Value = pair.Value});
            }

            saveData.ExpandedDatabases = new List<KeyValuePairStringList>();
            foreach (var connection in connections)
            {
                if (connection.Databases.Count <= 0) continue;
                var expandedDbs = connection.Databases
                    .Where(db => databaseStates.ContainsKey(db) && databaseStates[db])
                    .Select(db => db.Name)
                    .ToList();

                saveData.ExpandedDatabases.Add(
                    new KeyValuePairStringList {Key = connection.Path, Value = expandedDbs});
            }

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

            var json = JsonUtility.ToJson(saveData, true);
            EditorPrefs.SetString(SaveKey, json);
        }

        private void LoadSessionData()
        {
            if (!EditorPrefs.HasKey(SaveKey)) return;

            var json = EditorPrefs.GetString(SaveKey);
            var saveData = JsonUtility.FromJson<SaveData>(json);

            if (saveData?.Connections != null)
            {
                foreach (var connection in saveData.Connections.Select(path => new DatabaseConnection(path,
                    path.Contains("SSL")
                        ? DatabaseConnection.EConnectionType.MySQL
                        : DatabaseConnection.EConnectionType.SQLite)))
                {
                    connections.Add(connection);
                }
            }

            connectionStates.Clear();
            if (saveData == null) return;

            foreach (var pair in saveData.ExpandedConnections)
            {
                var connection = connections.FirstOrDefault(c => c.Path == pair.Key);
                if (connection != null)
                {
                    connectionStates[connection] = pair.Value;
                }
            }

            databaseStates.Clear();
            foreach (var db in from pair in saveData.ExpandedDatabases
                let connection = connections.FirstOrDefault(c => c.Path == pair.Key)
                where connection != null
                from db in connection.Databases
                where pair.Value.Contains(db.Name)
                select db)
            {
                databaseStates[db] = true;
            }

            if (saveData.OpenedTables.Count > 0)
            {
                foreach (var pair in saveData.OpenedTables)
                {
                    var connection = connections.FirstOrDefault(c => c.Path == pair.Key);
                    if (connection == null || connection.Databases.Count <= 0) continue;
                    selectedConnectionIndex = connections.IndexOf(connection);
                    selectedDatabaseIndex = 0;
                    selectedTableForContent = pair.Value;

                    var database = connection.Databases[selectedDatabaseIndex];
                    database.LoadTableContent(pair.Value);
                }
            }

            Repaint();
        }

        #endregion

        #region Json Operations

        private string GetDatabaseDataAsJson(Database database)
        {
            var output = GetTableNames(database).Select(table => GetTableDataAsJson(database, table)).ToList();
            return "[" + string.Join(",", output) + "]";
        }

        private string GetTableDataAsJson(Database database, string tableName)
        {
            var rows = new List<Dictionary<string, object>>();

            switch (database.ConnectionType)
            {
                case DatabaseConnection.EConnectionType.SQLite:
                    using (var connection = new SqliteConnection($"Data Source={database.ConnectionString};Version=3;"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = $"SELECT * FROM {tableName} LIMIT 50;";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    row[reader.GetName(i)] = value;
                                }

                                rows.Add(row);
                            }
                        }
                    }

                    break;
                case DatabaseConnection.EConnectionType.MySQL:
                    using (var connection = new MySqlConnection(database.ConnectionString))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = $"SELECT * FROM {tableName} LIMIT 50;"; // ограничим до 50 строк

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    row[reader.GetName(i)] = value;
                                }

                                rows.Add(row);
                            }
                        }
                    }

                    break;
            }

            var wrapper = new
            {
                table = tableName,
                rows
            };

            return JsonConvert.SerializeObject(wrapper, Formatting.Indented);
        }

        private string GetMultipleTablesDataAsJson(Database database, List<string> tables)
        {
            var output = tables.Select(table => GetTableDataAsJson(database, table)).ToList();
            return "[" + string.Join(",", output) + "]";
        }

        #endregion
        
        private List<string> GetTableNames(Database database)
        {
            var names = new List<string>();
            switch (database.ConnectionType)
            {
                case DatabaseConnection.EConnectionType.SQLite:
                    using (var connection = new SqliteConnection($"Data Source={database.ConnectionString};Version=3;"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                names.Add(reader.GetString(0));
                            }
                        }
                    }

                    break;
                case DatabaseConnection.EConnectionType.MySQL:
                    using (var connection = new SqliteConnection(database.ConnectionString))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                names.Add(reader.GetString(0));
                            }
                        }
                    }

                    break;
            }

            return names;
        }

        #region Serializable Classes (Referencable Data Containers)

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

        #endregion
    }
}