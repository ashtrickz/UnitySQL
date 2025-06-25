using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Mono.Data.Sqlite;
using Nhance.UnityDatabaseTool.Data;
using UnityEditor;
using UnityEngine;

namespace Nhance.UnityDatabaseTool.DatabaseProviders
{
    public class SqliteProvider : IDatabaseProvider
    {
        private SqliteConnection _connection;
        private readonly string _connectionString;
        private List<Table> _tables = new();
        public SqliteProvider(string connectionString) => _connectionString = connectionString;

        public string GetAssetPath(UnityEngine.Object assetObject)
        {
#if UNITY_EDITOR
            if (assetObject == null)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(assetObject);

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning(
                    $"Object '{assetObject.name}' is not saved asset. Path cant be get.",
                    assetObject);
                return null;
            }

            return path;
#else
        Debug.LogError("GetAssetPath is an Editor-only method.");
        return null;
#endif
        }

        public T LoadAssetFromPath<T>(string path) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                Debug.LogWarning($"Failed to get {typeof(T)} by path: {path}");
            }

            return asset;
#else
        Debug.LogError("LoadAssetFromPath is an Editor-only method.");
        return null;
#endif
        }

        public List<string> GetTableNames()
        {
            var names = new List<string>();
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                names.Add(rdr.GetString(0));
            return names;
        }

        public void RefreshTables()
        {
            LoadTables(null);
        }

        public void OpenConnection()
        {
            if (_connection != null)
                return;

            _connection = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            _connection.Open();

            if (_tables.Count > 0)
                LoadTables(_tables);
        }

        public void CloseConnection()
        {
            if (_connection == null)
                return;

            try
            {
                if (_connection.State != ConnectionState.Closed)
                    _connection.Close();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"SQLite: failed to close connection: {e.Message}");
            }
            finally
            {
                _connection.Dispose();
                _connection = null;
            }
        }

        public void RefreshConnection()
        {
            try
            {
                CloseConnection();
                OpenConnection();
                Debug.Log("SQLite: connection refreshed.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SQLite: failed to restore connection: {e.Message}");
            }
        }


        public void CreateTable(string tableName, List<Database.ColumnDefinition> columns, int primaryKeyIndex)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();

            var defs = columns.Select((col, i) =>
            {
                var type = col.Type;
                if (type == "Vector2" || type == "Vector3" || type == "GameObject" || type == "Sprite")
                    type = "TEXT";
                return i == primaryKeyIndex
                    ? $"{col.Name} {type} PRIMARY KEY"
                    : $"{col.Name} {type}";
            });

            using var cmd = new SqliteCommand(
                $"CREATE TABLE {tableName} ({string.Join(", ", defs)});", conn);
            cmd.ExecuteNonQuery();
        }

        public void ClearTable(string tableName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var command = new SqliteCommand($"DELETE FROM {tableName};", conn);
            command.ExecuteNonQuery();
        }

        public void DeleteTable(string tableName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = new SqliteCommand($"DROP TABLE IF EXISTS {tableName};", conn);
            cmd.ExecuteNonQuery();
        }

        public void LoadTables(List<Table> tables)
        {
            if (tables != null)
            {
                _tables.Clear();
                _tables = tables;
            }

            using var connection = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            connection.Open();

            using var command = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table';", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                _tables.Add(new Table(reader.GetString(0)));
            }
        }

        public List<Database.TableColumn> GetTableColumns(string tableName)
        {
            var cols = new List<Database.TableColumn>();
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cols.Add(new Database.TableColumn
                {
                    Name = rdr["name"].ToString(),
                    Type = rdr["type"].ToString(),
                    IsPrimaryKey = rdr["pk"].ToString() == "1"
                });
            return cols;
        }

        public List<string> GetColumnNames(string tableName)
        {
            var names = new List<string>();
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = new SqliteCommand($"PRAGMA table_info({tableName});", conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                names.Add(rdr.GetString(1));
            return names;
        }

        public string GetPrimaryKeyColumn(string tableName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = new SqliteCommand($"PRAGMA table_info({tableName});", conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                if (rdr.GetInt32(5) == 1)
                    return rdr.GetString(1);
            return null;
        }

        public string GetColumnType(string tableName, string columnName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = new SqliteCommand($"PRAGMA table_info({tableName});", conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                if (rdr.GetString(1) != columnName) continue;
                var colType = rdr.GetString(2).ToUpper();
                if (colType == "TEXT")
                {
                    try
                    {
                        using var sampleCmd =
                            new SqliteCommand(
                                $"SELECT {columnName} FROM {tableName} WHERE {columnName} IS NOT NULL LIMIT 1;", conn);
                        using var sr = sampleCmd.ExecuteReader();
                        if (sr.Read() && !sr.IsDBNull(0) && sr[0] is string s && s.Contains(","))
                        {
                            var parts = s.Split(',');
                            if (float.TryParse(parts[0], out _) && float.TryParse(parts[1], out _))
                            {
                                if (parts.Length == 2) return "Vector2";
                                if (parts.Length == 3 && float.TryParse(parts[2], out _)) return "Vector3";
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return rdr.GetString(2);
            }

            return "TEXT";
        }

        public bool CheckPrimaryKeyExists(string tableName, string primaryKeyColumn, string keyValue)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = new SqliteCommand(
                $"SELECT COUNT(*) FROM {tableName} WHERE {primaryKeyColumn} = @keyValue;", conn);
            cmd.Parameters.AddWithValue("@keyValue", keyValue);
            return (long) cmd.ExecuteScalar() > 0;
        }

        public void InsertRow(string tableName, Dictionary<string, object> rowData)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            var cols = string.Join(", ", rowData.Keys);
            var vals = string.Join(", ", rowData.Keys.Select(k => $"@{k}"));
            using var cmd = new SqliteCommand(
                $"INSERT INTO {tableName} ({cols}) VALUES ({vals});", conn);
            foreach (var kv in rowData)
                cmd.Parameters.AddWithValue($"@{kv.Key}", kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName,
            object newValue)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString}");
            conn.Open();

            var pk = GetPrimaryKeyColumn(tableName);
            if (string.IsNullOrEmpty(pk) || !rowData.ContainsKey(pk))
            {
                Debug.LogError(
                    $"[ERROR] Cannot update '{columnName}' in '{tableName}': No primary key found or its value is missing in the provided row data.");
                return;
            }
            
            object valueToDb;
            
            switch (newValue)
            {
                case Vector2 v2:
                    valueToDb = JsonUtility.ToJson(v2);
                    break;

                case Vector3 v3:
                    valueToDb = JsonUtility.ToJson(v3);
                    break;

                case Sprite sprite:
                    valueToDb = GetAssetPath(sprite);
                    break;

                case GameObject go:
#if UNITY_EDITOR
                    if (PrefabUtility.IsPartOfPrefabAsset(go))
                    {
                        valueToDb = GetAssetPath(go);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"GameObject '{go.name}' is not saved prefab. Skipping update.",
                            go);
                        valueToDb = null;
                    }
#else
                valueToDb = null;
#endif
                    break;
                
                default:
                    valueToDb = newValue;
                    break;
            }

            if (valueToDb == null && (newValue is Sprite || newValue is GameObject))
            {
                Debug.Log("Path was not acquired. Skipping...");
                return;
            }
            
            try
            {
                using var cmd = new SqliteCommand(
                    $"UPDATE `{tableName}` SET `{columnName}` = @newValue WHERE `{pk}` = @primaryKeyValue;", conn);
                
                cmd.Parameters.AddWithValue("@newValue", valueToDb);
                cmd.Parameters.AddWithValue("@primaryKeyValue", rowData[pk]);

                int rowsAffected = cmd.ExecuteNonQuery();
                Debug.Log($"[Database] Update successful for table '{tableName}'. Rows affected: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Database] Error while updating cell value for table '{tableName}': {ex.Message}");
            }
        }

        public void DeleteRow(string tableName, Dictionary<string, object> rowData)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            var pk = GetPrimaryKeyColumn(tableName);
            string query;
            var parameters = new List<SqliteParameter>();

            if (pk != null && rowData.ContainsKey(pk) && rowData[pk] != null)
            {
                query = $"DELETE FROM {tableName} WHERE {pk} = @PK;";
                parameters.Add(new SqliteParameter("@PK", rowData[pk]));
            }
            else
            {
                var conds = rowData
                    .Where(kv => kv.Value != null)
                    .Select(kv => $"{kv.Key} = @{kv.Key}");
                query = $"DELETE FROM {tableName} WHERE {string.Join(" AND ", conds)} LIMIT 1;";
                parameters = rowData
                    .Where(kv => kv.Value != null)
                    .Select(kv => new SqliteParameter($"@{kv.Key}", kv.Value))
                    .ToList();
            }

            using var cmd = new SqliteCommand(query, conn);
            parameters.ForEach(p => cmd.Parameters.Add(p));
            cmd.ExecuteNonQuery();
        }

        public void AddColumn(string tableName, string columnName, string columnType)
        {
            var type = columnType;
            if (type == "Vector2" || type == "Vector3" || type == "GameObject" || type == "Sprite")
                type = "TEXT";
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = new SqliteCommand(
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {type};", conn);
            cmd.ExecuteNonQuery();
        }

        public void ModifyColumn(string tableName, string oldName, string newName, string newType, bool isPrimaryKey)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                var columns = GetTableColumns(tableName);
                string tempOldPrimaryKey = columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name;

                var tempTableName = $"{tableName}_temp";
                var createTableBuilder = new StringBuilder($"CREATE TABLE {tempTableName} (");
                var newColumnDefinitions = new List<string>();

                string newPrimaryKey =
                    isPrimaryKey ? newName : (tempOldPrimaryKey != oldName ? tempOldPrimaryKey : null);

                foreach (var col in columns)
                {
                    var currentOldName = col.Name;
                    var definition = currentOldName == oldName
                        ? $"{newName} {newType}"
                        : $"{currentOldName} {col.Type}";
                    newColumnDefinitions.Add(definition);
                }

                createTableBuilder.Append(string.Join(", ", newColumnDefinitions));
                if (!string.IsNullOrEmpty(newPrimaryKey))
                {
                    createTableBuilder.Append($", PRIMARY KEY({newPrimaryKey}");
                    if (newType.ToUpper() == "INTEGER")
                    {
                        createTableBuilder.Append(" AUTOINCREMENT");
                    }

                    createTableBuilder.Append(")");
                }

                createTableBuilder.Append(");");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = createTableBuilder.ToString();
                    cmd.ExecuteNonQuery();
                }

                var originalColumnNames = columns.Select(c => c.Name).ToList();
                var insertColumnNames = columns.Select(c => c.Name == oldName ? newName : c.Name).ToList();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText =
                        $"INSERT INTO {tempTableName} ({string.Join(", ", insertColumnNames)}) SELECT {string.Join(", ", originalColumnNames)} FROM {tableName};";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = $"DROP TABLE {tableName};";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SQLite Provider] Failed to modify column in table '{tableName}'. Error: {e.Message}");
                transaction.Rollback();
            }
        }

        public void DeleteColumn(string tableName, string columnName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                var columns = GetTableColumns(tableName);
                var remainingColumns =
                    columns.Where(c => !c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (remainingColumns.Count == columns.Count)
                {
                    Debug.LogWarning($"[SQLite Provider] Column '{columnName}' not found in table '{tableName}'.");
                    return;
                }

                string pkName = remainingColumns.FirstOrDefault(c => c.IsPrimaryKey)?.Name;

                var tempTableName = $"{tableName}_temp";
                var createTableBuilder = new StringBuilder($"CREATE TABLE {tempTableName} (");
                var newColumnDefinitions = remainingColumns.Select(col => $"{col.Name} {col.Type}").ToList();
                createTableBuilder.Append(string.Join(", ", newColumnDefinitions));

                if (!string.IsNullOrEmpty(pkName))
                {
                    createTableBuilder.Append($", PRIMARY KEY({pkName})");
                }

                createTableBuilder.Append(");");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = createTableBuilder.ToString();
                    cmd.ExecuteNonQuery();
                }

                var insertColumnNames = string.Join(", ", remainingColumns.Select(c => c.Name));

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText =
                        $"INSERT INTO {tempTableName} ({insertColumnNames}) SELECT {insertColumnNames} FROM {tableName};";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = $"DROP TABLE {tableName};";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[SQLite Provider] Failed to delete column '{columnName}' from table '{tableName}'. Error: {e.Message}");
                transaction.Rollback();
            }
        }

        public void ChangeColumnType(string tableName, string oldColumnName, string newColumnName, string newColumnType)
        {
        }

        public void MakePrimaryKey(string tableName, string newPrimaryKey)
        {
            try
            {
                var columnToModify = GetTableColumns(tableName)
                    .FirstOrDefault(c => c.Name.Equals(newPrimaryKey, StringComparison.OrdinalIgnoreCase));

                if (columnToModify != null)
                    ModifyColumn(tableName, newPrimaryKey, newPrimaryKey, columnToModify.Type, true);
                else
                    Debug.LogError(
                        $"[SQLite Provider] Column '{newPrimaryKey}' not found in table '{tableName}'. Cannot make it a primary key.");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[SQLite Provider] Failed to make column '{newPrimaryKey}' a primary key in table '{tableName}'. Error: {e.Message}");
            }
        }

        public bool IsAutoIncrement(string tableName, string columnName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{tableName}');";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    var type = reader.GetString(2).ToUpper();
                    var isPrimaryKey = reader.GetInt32(5) > 0;

                    return type == "INTEGER" && isPrimaryKey && !tableName.Equals("sqlite_sequence");
                }
            }

            return false;
        }
    }
}