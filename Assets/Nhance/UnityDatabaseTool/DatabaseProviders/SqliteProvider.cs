using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Mono.Data.Sqlite;
using Nhance.UnityDatabaseTool.Data;
using UnityEngine;

namespace Nhance.UnityDatabaseTool.DatabaseProviders
{
    public class SqliteProvider : IDatabaseProvider
    {
        private SqliteConnection _connection;
        private readonly string _connectionString;
        private List<Table> _tables = new();
        public SqliteProvider(string connectionString) => _connectionString = connectionString;

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
            using var command = new SqliteCommand($"TRUNCATE TABLE `{tableName}`;", conn);
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
                            new SqliteCommand($"SELECT {columnName} FROM {tableName} LIMIT 1;", conn);
                        using var sr = sampleCmd.ExecuteReader();
                        if (sr.Read() && sr[0] is string s && s.Contains(","))
                        {
                            var parts = s.Split(',');
                            if (parts.Length == 2) return "Vector2";
                            if (parts.Length == 3) return "Vector3";
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
            string newValue)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            var pk = GetPrimaryKeyColumn(tableName);
            if (pk == null || !rowData.ContainsKey(pk))
            {
                Debug.LogError($"[ERROR] Cannot update '{columnName}' in '{tableName}': No primary key found.");
                return;
            }

            object converted = newValue;
            var orig = rowData[columnName];
            if (orig is long)
                converted = long.TryParse(newValue, out var lv) ? lv : newValue;
            else if (orig is int)
                converted = int.TryParse(newValue, out var iv) ? iv : newValue;
            else if (orig is double)
                converted = double.TryParse(newValue, out var dv) ? dv : newValue;

            using var cmd = new SqliteCommand(
                $"UPDATE {tableName} SET {columnName} = @val WHERE {pk} = @pk;", conn);
            cmd.Parameters.AddWithValue("@val", converted);
            cmd.Parameters.AddWithValue("@pk", rowData[pk]);
            cmd.ExecuteNonQuery();
        }

        public void DeleteRow(string tableName, Dictionary<string, object> rowData)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            var pk = GetPrimaryKeyColumn(tableName);
            string query;
            var parameters = new List<SqliteParameter>();

            if (pk != null && rowData.ContainsKey(pk))
            {
                query = $"DELETE FROM {tableName} WHERE {pk} = @PK;";
                parameters.Add(new SqliteParameter("@PK", rowData[pk]));
            }
            else
            {
                var conds = rowData
                    .Where(kv => kv.Value != null)
                    .Select(kv => $"{kv.Key} = @{kv.Key}");
                query = $"DELETE FROM {tableName} WHERE {string.Join(" AND ", conds)};";
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
            if (type == "Vector2" || type == "Vector3")
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
            
            var columns = new List<Tuple<string, string, bool>>();
            var columnNames = new List<string>();
            string tempOldPrimaryKey = null;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info('{tableName}');";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var name = reader.GetString(1);
                        var type = reader.GetString(2);
                        var pk = reader.GetInt32(5) == 1;
                        columns.Add(new Tuple<string, string, bool>(name, type, pk));
                        columnNames.Add(name);
                        if (pk) tempOldPrimaryKey = name;
                    }
                }
            }
            
            var tempTableName = $"{tableName}_temp";
            var createTableBuilder = new System.Text.StringBuilder($"CREATE TABLE {tempTableName} (");
            var newColumnDefinitions = new List<string>();
            
            string newPrimaryKey = isPrimaryKey ? newName : (tempOldPrimaryKey != oldName ? tempOldPrimaryKey : null);

            foreach (var col in columns)
            {
                var currentOldName = col.Item1;

                newColumnDefinitions.Add(currentOldName == oldName
                    ? $"'{newName}' {newType}"
                    : $"'{currentOldName}' {col.Item2}");
            }
            
            createTableBuilder.Append(string.Join(", ", newColumnDefinitions));
            if (!string.IsNullOrEmpty(newPrimaryKey))
            {
                createTableBuilder.Append($", PRIMARY KEY('{newPrimaryKey}' AUTOINCREMENT)");
            }

            createTableBuilder.Append(");");
            
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = createTableBuilder.ToString();
                cmd.ExecuteNonQuery();
            }
            
            var insertColumnNames = columnNames.Select(c => c == oldName ? newName : c);
            var selectColumnNames = columnNames;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    $"INSERT INTO {tempTableName} ({string.Join(", ", insertColumnNames.Select(n => $"'{n}'"))}) SELECT {string.Join(", ", selectColumnNames.Select(n => $"'{n}'"))} FROM {tableName};";
                cmd.ExecuteNonQuery();
            }
            
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"DROP TABLE {tableName};";
                cmd.ExecuteNonQuery();
            }
            
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public void DeleteColumn(string tableName, string columnName)
        {
            using var conn = new SqliteConnection($"Data Source={_connectionString};Version=3;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
            cmd.ExecuteNonQuery();
        }

        public void ChangeColumnType(string tableName, string oldColumnName, string newColumnName, string newColumnType)
        {
        }

        public void MakePrimaryKey(string tableName, string newPrimaryKey)
        {
        }
    }
}