using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nhance.USQL.Editor
{
    public interface IModalContent
    {
        void OnGUI();

        void OnClose()
        {
        }
    }

    public class GenericModalWindow : EditorWindow
    {
        private IModalContent _content;
        private Vector2 _scroll;

        public static void Show(IModalContent content, string title = "Modal", Vector2? size = null)
        {
            var wnd = CreateInstance<GenericModalWindow>();
            wnd._content = content;
            wnd.titleContent = new GUIContent(title);
            if (size.HasValue)
                wnd.minSize = wnd.maxSize = size.Value;
            wnd.ShowUtility();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _content.OnGUI();
            EditorGUILayout.EndScrollView();
        }

        private void OnDestroy() =>
            _content?.OnClose();
    }

    // Modal Content //

    public class AddColumnContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private string _columnName = string.Empty;
        private int _typeIndex;

        private static readonly string[] _types =
            {"TEXT", "VARCHAR", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"};

        public AddColumnContent(Database db, string table)
        {
            _db = db;
            _table = table;
        }

        public void OnGUI()
        {
            GUILayout.Label("Add Column", EditorStyles.boldLabel);
            _columnName = EditorGUILayout.TextField("Column Name", _columnName);
            _typeIndex = EditorGUILayout.Popup("Column Type", _typeIndex, _types);
            GUILayout.Space(10);
            if (GUILayout.Button("Add", GUILayout.Height(25)))
            {
                if (!string.IsNullOrEmpty(_columnName))
                    _db.AddColumn_Maria(_table, _columnName, _types[_typeIndex]);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class DuplicateRowContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly Dictionary<string, object> _row;

        public DuplicateRowContent(Database db, string table, Dictionary<string, object> row)
        {
            _db = db;
            _table = table;
            _row = row;
        }

        public void OnGUI()
        {
            GUILayout.Label($"Duplicate row in '{_table}'", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            if (GUILayout.Button("Duplicate", GUILayout.Height(25)))
            {
                _db.InsertRow_Maria(_table, _row);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class AddRowContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly List<string> _cols;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public AddRowContent(Database db, string table)
        {
            _db = db;
            _table = table;
            
            _cols = db.ConnectionType == DatabaseConnection.EConnectionType.MySQL
                ? db.GetColumnNames_Maria(table)
                : db.GetColumnNames_Lite(table);
            
            foreach (var col in _cols) _values[col] = string.Empty;
        }

        public void OnGUI()
        {
            GUILayout.Label($"Add Row to '{_table}'", EditorStyles.boldLabel);
            foreach (var col in _cols)
            {
                _values[col] = EditorGUILayout.TextField(col, _values[col]);
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Insert", GUILayout.Height(25)))
            {
                var row = new Dictionary<string, object>();
                foreach (var col in _cols) row[col] = _values[col];
                _db.InsertRow_Maria(_table, row);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class ChangeColumnContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly string _oldName;
        private string _newName;
        private int _newTypeIndex;
        private bool _isPk;

        private static readonly string[] _types =
            {"TEXT", "VARCHAR", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"};

        public ChangeColumnContent(Database db, string table, string column)
        {
            _db = db;
            _table = table;
            _oldName = column;
            _newName = column;
        }

        public void OnGUI()
        {
            GUILayout.Label($"Change Column '{_oldName}'", EditorStyles.boldLabel);
            _newName = EditorGUILayout.TextField("Column Name", _newName);
            _newTypeIndex = EditorGUILayout.Popup("Column Type", _newTypeIndex, _types);
            _isPk = EditorGUILayout.Toggle("Primary Key", _isPk);
            GUILayout.Space(10);
            if (GUILayout.Button("Save", GUILayout.Height(25)))
            {
                _db.ModifyColumn_Maria(_table, /* index= */ 0, _newName, _types[_newTypeIndex], _isPk);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class ChangeValueContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly string _col;
        private readonly Dictionary<string, object> _row;
        private string _newVal;

        public ChangeValueContent(Database db, string table, Dictionary<string, object> row, string column)
        {
            _db = db;
            _table = table;
            _row = row;
            _col = column;
            _newVal = row[column]?.ToString() ?? string.Empty;
        }

        public void OnGUI()
        {
            GUILayout.Label($"Change '{_col}' in '{_table}'", EditorStyles.boldLabel);
            _newVal = EditorGUILayout.TextField(_col, _newVal);
            GUILayout.Space(10);
            if (GUILayout.Button("Update", GUILayout.Height(25)))
            {
                _db.UpdateCellValue_Maria(_table, _row, _col, _newVal);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class ConfirmationContent : IModalContent
    {
        private readonly string _message;
        private readonly Action _onConfirm;

        public ConfirmationContent(string message, Action onConfirm)
        {
            _message = message;
            _onConfirm = onConfirm;
        }

        public void OnGUI()
        {
            GUILayout.Label(_message, EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            if (GUILayout.Button("Yes", GUILayout.Height(25)))
            {
                _onConfirm?.Invoke();
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }
    
    public class CreateDatabaseContent : IModalContent
    {
        private readonly UnitySqlManager _manager;
        private readonly DatabaseConnection _connection;
        private string _dbName;

        public CreateDatabaseContent(UnitySqlManager manager, DatabaseConnection connection)
        {
            _manager = manager;
            _connection = connection;
        }

        public void OnGUI()
        {
            GUILayout.Label("Create a New Database", EditorStyles.boldLabel);
            GUILayout.Space(5);
            _dbName = EditorGUILayout.TextField("Database Name:", _dbName);

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create", GUILayout.Height(25)))
            {
                if (!string.IsNullOrEmpty(_dbName))
                {
                    _connection.CreateDatabase(_dbName);
                    _manager.SaveSessionData();
                    CloseWindow();
                }
                else
                {
                    Debug.LogError("[ERROR] Database name cannot be empty.");
                }
            }

            if (GUILayout.Button("Cancel", GUILayout.Height(25)))
            {
                CloseWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class CreateTableContent : IModalContent
    {
        private readonly Database _database;
        private string _tableName;
        private readonly List<Database.ColumnDefinition> _columns = new List<Database.ColumnDefinition>();
        private int _primaryKeyIndex = -1;

        private static readonly string[] _columnTypes =
            {"TEXT", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"};

        public CreateTableContent(Database database)
        {
            _database = database;
        }

        public void OnGUI()
        {
            GUILayout.Label("Create New Table", EditorStyles.boldLabel);
            _tableName = EditorGUILayout.TextField("Table Name:", _tableName);

            GUILayout.Space(10);
            GUILayout.Label("Columns:", EditorStyles.boldLabel);

            for (int i = 0; i < _columns.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                _columns[i].Name = EditorGUILayout.TextField(_columns[i].Name, GUILayout.Width(150));
                _columns[i].Type = _columnTypes[
                    EditorGUILayout.Popup(
                        System.Array.IndexOf(_columnTypes, _columns[i].Type),
                        _columnTypes, GUILayout.Width(100)
                    )
                ];

                EditorGUILayout.LabelField("Is Primary Key:", GUILayout.Width(90));
                bool isPk = (_primaryKeyIndex == i);
                bool newPk = EditorGUILayout.Toggle(isPk, GUILayout.Width(20));
                if (newPk && !isPk) _primaryKeyIndex = i;
                else if (!newPk && isPk) _primaryKeyIndex = -1;

                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    if (_primaryKeyIndex == i) _primaryKeyIndex = -1;
                    _columns.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(25)))
            {
                _columns.Add(new Database.ColumnDefinition {Name = "", Type = "TEXT"});
            }

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Table", GUILayout.Height(25)))
            {
                if (!string.IsNullOrEmpty(_tableName) && _columns.Count > 0)
                {
                    _database.CreateTable_Maria(_tableName, _columns, _primaryKeyIndex);
                    CloseWindow();
                }
                else
                {
                    Debug.LogError("[ERROR] Table name and at least one column are required.");
                }
            }

            if (GUILayout.Button("Cancel", GUILayout.Height(25)))
            {
                CloseWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }


    public class DeleteColumnConfirmationContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly string _col;

        public DeleteColumnConfirmationContent(Database db, string table, string col)
        {
            _db = db;
            _table = table;
            _col = col;
        }

        public void OnGUI()
        {
            GUILayout.Label($"Delete column '{_col}' from '{_table}'?", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            if (GUILayout.Button("Delete", GUILayout.Height(25)))
            {
                _db.DeleteColumn_Maria(_table, _col);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class DeleteRowConfirmationContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly Dictionary<string, object> _row;

        public DeleteRowConfirmationContent(Database db, string table, Dictionary<string, object> row)
        {
            _db = db;
            _table = table;
            _row = row;
        }

        public void OnGUI()
        {
            GUILayout.Label($"Delete this row from '{_table}'?", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            if (GUILayout.Button("Delete", GUILayout.Height(25)))
            {
                DeleteRowConfirmationWindow.DeleteRow(_db, _table, _row);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow() => EditorWindow.focusedWindow.Close();
    }

    public class DeleteRowConfirmationWindow : EditorWindow
    {
        private static Database database;
        private static string tableName;
        private static Dictionary<string, object> rowData;

        public static void DeleteRow(Database db, string tblName, Dictionary<string, object> row)
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
}