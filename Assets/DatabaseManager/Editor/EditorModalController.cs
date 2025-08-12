using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseManager.Data;
using UnityEditor;
using UnityEngine;

namespace DatabaseManager.Editor
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

        private UnityDatabaseEditor _editor;


        private static readonly string[] _types =
            {"TEXT", "VARCHAR", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"};

        public AddColumnContent(UnityDatabaseEditor editor, Database db, string table)
        {
            _editor = editor;
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
                    _db.AddColumn(_table, _columnName, _types[_typeIndex]);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow()
        {
            _db.RefreshTable(_editor.SelectedTableName);
            EditorWindow.focusedWindow.Close();
        }
    }

    public class DuplicateRowContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly Dictionary<string, object> _row;
        private UnityDatabaseEditor _editor;

        public DuplicateRowContent(UnityDatabaseEditor editor, Database db, string table,
            Dictionary<string, object> row)
        {
            _editor = editor;
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
                _db.InsertRow(_table, _row);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow()
        {
            _db.RefreshTable(_editor.SelectedTableName);
            EditorWindow.focusedWindow.Close();
        }
    }

    public class AddRowContent : IModalContent
    {
        private readonly UnityDatabaseEditor _editor;
        private readonly Database _db;
        private readonly string _table;
        private readonly List<Database.TableColumn> _cols;

        private readonly Dictionary<string, object>
            _values = new Dictionary<string, object>();

        public AddRowContent(UnityDatabaseEditor editor, Database db, string table)
        {
            _editor = editor;
            _db = db;
            _table = table;

            _cols = db.GetTableColumns(table);

            foreach (var col in _cols)
            {
                if (col.IsPrimaryKey && db.IsAutoIncrement(table, col.Name))
                {
                    continue;
                }

                _values[col.Name] = GetDefaultValueForType(col.Type);
            }
        }

        public void OnGUI()
        {
            GUILayout.Label($"Add Row to '{_table}'", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Column", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Value", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            foreach (var col in _cols)
            {
                if (col.IsPrimaryKey && _db.IsAutoIncrement(_table, col.Name))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent(col.Name, "Primary Key - Auto-increment"),
                        GUILayout.Width(150));
                    EditorGUILayout.LabelField(col.Type, GUILayout.Width(120));
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField("(Auto)");
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    continue;
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(col.Name, GUILayout.Width(150));
                EditorGUILayout.LabelField(col.Type, GUILayout.Width(120));

                DrawInputForColumn(col);

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Insert", GUILayout.Height(25)))
            {
                _db.InsertRow(_table, _values);
                CloseWindow();
            }
        }

        private void DrawInputForColumn(Database.TableColumn col)
        {
            var baseType = GetBaseType(col.Type);
            var colName = col.Name;

            switch (baseType)
            {
                case "INT":
                    _values[colName] = EditorGUILayout.IntField((int) (_values[colName] ?? 0));
                    break;

                case "REAL":
                case "DECIMAL":
                    _values[colName] = EditorGUILayout.FloatField((float) (_values[colName] ?? 0f));
                    break;

                case "DATE":
                    EditorGUILayout.BeginHorizontal();
                    _values[colName] = EditorGUILayout.TextField(_values[colName] as string ?? "");
                    if (GUILayout.Button("Today", GUILayout.Width(50)))
                    {
                        _values[colName] = DateTime.Now.ToString("yyyy-MM-dd");
                    }

                    EditorGUILayout.EndHorizontal();
                    break;

                case "DATETIME":
                    EditorGUILayout.BeginHorizontal();
                    _values[colName] = EditorGUILayout.TextField(_values[colName] as string ?? "");
                    if (GUILayout.Button("Now", GUILayout.Width(50)))
                    {
                        _values[colName] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    }

                    EditorGUILayout.EndHorizontal();
                    break;

                case "GameObject":
                    _values[colName] =
                        EditorGUILayout.ObjectField(_values[colName] as GameObject, typeof(GameObject), false);
                    break;

                case "Sprite":
                    _values[colName] = EditorGUILayout.ObjectField(_values[colName] as Sprite, typeof(Sprite), false);
                    break;

                case "Vector2":
                    _values[colName] = EditorGUILayout.Vector2Field("", (Vector2) (_values[colName] ?? Vector2.zero));
                    break;

                case "Vector3":
                    _values[colName] = EditorGUILayout.Vector3Field("", (Vector3) (_values[colName] ?? Vector3.zero));
                    break;

                default:
                    _values[colName] = EditorGUILayout.TextField(_values[colName] as string ?? "");
                    break;
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow()
        {
            _db.RefreshTable(_editor.SelectedTableName);
            EditorWindow.focusedWindow.Close();
        }

        private string GetBaseType(string fullType)
        {
            if (string.IsNullOrEmpty(fullType)) return "TEXT";
            int parenthesisIndex = fullType.IndexOf('(');
            if (parenthesisIndex > 0)
            {
                return fullType.Substring(0, parenthesisIndex).Trim().ToUpper();
            }

            return fullType.Trim().ToUpper();
        }

        private object GetDefaultValueForType(string fullType)
        {
            switch (GetBaseType(fullType))
            {
                case "INT": return 0;
                case "REAL":
                case "DECIMAL": return 0f;
                case "Vector2": return Vector2.zero;
                case "Vector3": return Vector3.zero;
                case "GameObject":
                case "Sprite": return null;
                default: return string.Empty;
            }
        }
    }


    public class ChangeColumnContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly string _oldName;
        private string _newName;
        private int _newTypeIndex;
        private bool _isPk;
        private UnityDatabaseEditor _editor;


        private static readonly string[] _types =
            {"TEXT", "VARCHAR", "INTEGER", "REAL", "BLOB", "GameObject", "Sprite", "Vector2", "Vector3"};

        public ChangeColumnContent(UnityDatabaseEditor editor, Database db, string table, string column)
        {
            _editor = editor;
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
                _db.ModifyColumn(_table, /* index= */ _oldName, _newName, _types[_newTypeIndex], _isPk);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow()
        {
            _db.RefreshTable(_editor.SelectedTableName);
            EditorWindow.focusedWindow.Close();
        }
    }

    public class ChangeValueContent : IModalContent
{
    private readonly Database _db;
    private readonly UnityDatabaseEditor _editor;
    private readonly string _table;
    private readonly string _col;
    private readonly Dictionary<string, object> _row;

    // Храним новое значение как object, чтобы поддерживать разные типы
    private object _newValue;
    // Храним тип колонки, чтобы знать, какой UI рисовать
    private readonly string _columnType;

    public ChangeValueContent(UnityDatabaseEditor editor, Database db, string table, Dictionary<string, object> row, string column)
    {
        _editor = editor;
        _db = db;
        _table = table;
        _row = row;
        _col = column;
        
        // Получаем оригинальное значение
        _newValue = row[column];
        
        // Получаем тип колонки. Предполагается, что у вас есть метод для этого.
        // Если нет, вам нужно будет его добавить.
        _columnType = _db.GetColumnType(_table, _col);
    }

    public void OnGUI()
    {
        GUILayout.Label($"Change '{_col}' in '{_table}' (Type: {_columnType})", EditorStyles.boldLabel);

        // Используем switch для отображения правильного поля редактора
        // в зависимости от типа данных колонки.
        switch (_columnType)
        {
            case "INTEGER":
                // SQLite возвращает long, поэтому конвертируем из long.
                int currentInt = Convert.ToInt32(_newValue);
                _newValue = EditorGUILayout.IntField(_col, currentInt);
                break;
            
            case "REAL":
            case "DECIMAL":
                // SQLite возвращает double, конвертируем в float для редактора.
                float currentFloat = Convert.ToSingle(_newValue);
                _newValue = EditorGUILayout.FloatField(_col, currentFloat);
                break;

            case "Vector2":
                // Пытаемся безопасно привести к Vector2, если не получается - используем Vector2.zero
                Vector2 currentVec2 = (_newValue is Vector2 v2) ? v2 : Vector2.zero;
                _newValue = EditorGUILayout.Vector2Field(_col, currentVec2);
                break;
                
            case "Vector3":
                Vector3 currentVec3 = (_newValue is Vector3 v3) ? v3 : Vector3.zero;
                _newValue = EditorGUILayout.Vector3Field(_col, currentVec3);
                break;
            
            // Для всех остальных типов (TEXT, VARCHAR, DATE, BLOB и кастомных) используем текстовое поле
            default:
                string currentStr = _newValue?.ToString() ?? string.Empty;
                _newValue = EditorGUILayout.TextField(_col, currentStr);
                break;
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Update", GUILayout.Height(25)))
        {
            // Теперь _newValue имеет правильный тип (int, float, Vector2, string и т.д.)
            _db.UpdateCellValue(_table, _row, _col, _newValue);
            CloseWindow();
        }
    }

    public void OnClose() { }

    private void CloseWindow()
    {
        // Обновляем отображение таблицы после изменения
        if (_editor != null && !string.IsNullOrEmpty(_editor.SelectedTableName))
        {
            _db.RefreshTable(_editor.SelectedTableName);
        }
        
        var window = EditorWindow.focusedWindow;
        if (window != null)
        {
            window.Close();
        }
    }
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
        private readonly UnityDatabaseEditor _manager;
        private readonly DatabaseConnection _connection;
        private string _dbName;

        public CreateDatabaseContent(UnityDatabaseEditor manager, DatabaseConnection connection)
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
        private string _tableName = "";
        private Vector2 _scrollPosition;
        
        private readonly List<Database.ColumnDefinition> _columns = new List<Database.ColumnDefinition>();
        private int _primaryKeyIndex = -1;
        
        private static readonly string[] _columnTypes =
        {
            "TEXT", "INTEGER", "REAL", "BLOB", "VARCHAR", "DECIMAL", "DATE",
            "GameObject", "Sprite", "Vector2", "Vector3"
        };

        public CreateTableContent(Database database)
        {
            _database = database;
            AddNewColumn();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Create New Table", EditorStyles.boldLabel);
            GUILayout.Space(5);
            _tableName = EditorGUILayout.TextField("Table Name", _tableName);
            GUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Column Name", EditorStyles.boldLabel, GUILayout.MinWidth(150),
                GUILayout.MaxWidth(250));
            EditorGUILayout.LabelField("Data Type", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Length", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("PK", EditorStyles.boldLabel, GUILayout.Width(25));
            EditorGUILayout.LabelField("Not Null", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Unique", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Action", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                EditorGUILayout.BeginHorizontal();
                
                column.Name = EditorGUILayout.TextField(column.Name, GUILayout.MinWidth(150), GUILayout.MaxWidth(250));
                
                int typeIndex = System.Array.IndexOf(_columnTypes, column.Type);
                if (typeIndex == -1) typeIndex = 0;
                column.Type = _columnTypes[EditorGUILayout.Popup(typeIndex, _columnTypes, GUILayout.Width(100))];
                
                if (column.Type == "VARCHAR")
                {
                    column.Length = EditorGUILayout.IntField(column.Length, GUILayout.Width(50));
                }
                else
                {
                    GUILayout.Space(54);
                }

                // Primary Key (PK)
                bool isPk = (_primaryKeyIndex == i);
                bool newIsPk = EditorGUILayout.Toggle(isPk, GUILayout.Width(25));
                if (newIsPk && !isPk)
                {
                    _primaryKeyIndex = i;
                    column.IsNotNull = true;
                    column.IsUnique = true;
                }
                else if (!newIsPk && isPk)
                {
                    _primaryKeyIndex = -1;
                }
                
                GUI.enabled = !isPk;
                column.IsNotNull = EditorGUILayout.Toggle(column.IsNotNull, GUILayout.Width(60));
                column.IsUnique = EditorGUILayout.Toggle(column.IsUnique, GUILayout.Width(50));
                GUI.enabled = true;

                
                if (GUILayout.Button("-", GUILayout.Width(50)))
                {
                    if (_primaryKeyIndex == i) _primaryKeyIndex = -1;
                    else if (_primaryKeyIndex > i) _primaryKeyIndex--;

                    _columns.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Add Column", GUILayout.Height(25)))
            {
                AddNewColumn();
            }

            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginHorizontal();
            
            bool canCreateTable = !string.IsNullOrWhiteSpace(_tableName) && _columns.Count > 0;
            GUI.enabled = canCreateTable;

            if (GUILayout.Button("Create Table", GUILayout.Height(30)))
            {
                if (_columns.GroupBy(c => c.Name.ToLower()).Any(g => g.Count() > 1))
                {
                    EditorUtility.DisplayDialog("Error", "Column names must be unique.", "OK");
                }
                else
                {
                    _database.CreateTable(_tableName, _columns, _primaryKeyIndex);
                    CloseWindow();
                }
            }

            GUI.enabled = true;

            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                CloseWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewColumn()
        {
            _columns.Add(new Database.ColumnDefinition {Name = "", Type = "TEXT", Length = 255});
        }

        public void OnClose()
        {
        }

        private void CloseWindow()
        {
            var window = EditorWindow.focusedWindow;
            if (window != null)
            {
                window.Close();
            }
        }
    }


    public class DeleteColumnConfirmationContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly string _col;
        private UnityDatabaseEditor _editor;

        public DeleteColumnConfirmationContent(UnityDatabaseEditor editor, Database db, string table, string col)
        {
            _editor = editor;
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
                _db.DeleteColumn(_table, _col);
                CloseWindow();
            }
        }

        public void OnClose()
        {
        }

        private void CloseWindow()
        {
            _db.RefreshTable(_editor.SelectedTableName);
            EditorWindow.focusedWindow.Close();
        }
    }

    public class DeleteRowConfirmationContent : IModalContent
    {
        private readonly Database _db;
        private readonly string _table;
        private readonly Dictionary<string, object> _row;
        private UnityDatabaseEditor _editor;

        public DeleteRowConfirmationContent(UnityDatabaseEditor editor, Database db, string table,
            Dictionary<string, object> row)
        {
            _editor = editor;
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

        private void CloseWindow()
        {
            _db.RefreshTable(_editor.SelectedTableName);
            EditorWindow.focusedWindow.Close();
        }
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
                database.DeleteRowFromTable(tableName, rowData);
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