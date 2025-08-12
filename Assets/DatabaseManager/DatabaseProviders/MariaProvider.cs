using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DatabaseManager.Data;
using MySqlConnector;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DatabaseManager.DatabaseProviders
{
    public class MariaProvider : IDatabaseProvider
    {
        private MySqlConnection _connection;
        private readonly string _connectionString;
        private readonly string _databaseName;

        private List<Table> _tables = new();

        public MariaProvider(string connectionString)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString);
                _databaseName = builder.Database;
                _connectionString = connectionString;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Could not parse database name from connection string: {connectionString}. Error: {e.Message}");
                _databaseName = string.Empty;
            }
        }

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
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand("SHOW TABLES;", conn);
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

            _connection = new MySqlConnection(_connectionString);
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
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to close connection: {e.Message}");
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
                Debug.Log("MySQL: connection refreshed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"MySQL: failed to restore connection: {e.Message}");
            }
        }

        public void CreateTable(string tableName, List<Database.ColumnDefinition> columns, int primaryKeyIndex)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            var defs = new List<string>();
            string pkName = null;
            for (int i = 0; i < columns.Count; i++)
            {
                var type = columns[i].Type;
                if (type == "Vector2" || type == "Vector3" || type == "GameObject" || type == "Sprite")
                    type = "TEXT";
                defs.Add($"`{columns[i].Name}` {type}");
                if (i == primaryKeyIndex) pkName = columns[i].Name;
            }

            if (!string.IsNullOrEmpty(pkName))
                defs.Add($"PRIMARY KEY(`{pkName}`)");

            using var cmd = new MySqlCommand(
                $"CREATE TABLE `{tableName}` ({string.Join(", ", defs)});", conn);
            cmd.ExecuteNonQuery();
        }

        public void ClearTable(string tableName)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand($"TRUNCATE TABLE `{tableName}`;", connection);
            command.ExecuteNonQuery();
        }

        public void DeleteTable(string tableName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand($"DROP TABLE IF EXISTS `{tableName}`;", conn);
            cmd.ExecuteNonQuery();
        }

        public void LoadTables(List<Table> tables)
        {
            if (tables != null) _tables = tables;

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand("SHOW TABLES;", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                _tables.Add(new Table(reader.GetString(0)));
            }
        }

        public List<Database.TableColumn> GetTableColumns(string tableName)
        {
            var cols = new List<Database.TableColumn>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(@"
            SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_KEY
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION;", conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cols.Add(new Database.TableColumn
                {
                    Name = rdr.GetString("COLUMN_NAME"),
                    Type = rdr.GetString("COLUMN_TYPE").ToUpper(),
                    IsPrimaryKey = rdr.GetString("COLUMN_KEY") == "PRI"
                });
            return cols;
        }

        public List<string> GetColumnNames(string tableName)
        {
            var names = new List<string>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION;", conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                names.Add(rdr.GetString(0));
            return names;
        }

        public string GetPrimaryKeyColumn(string tableName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_KEY = 'PRI'
            LIMIT 1;", conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            using var rdr = cmd.ExecuteReader();
            return rdr.Read() ? rdr.GetString(0) : null;
        }

        public string GetColumnType(string tableName, string columnName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(@"
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column
            LIMIT 1;", conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            cmd.Parameters.AddWithValue("@column", columnName);
            var type = (cmd.ExecuteScalar()?.ToString() ?? "TEXT").ToUpper();

            // TODO: Vector2/3 inference

            return type;
        }

        public bool CheckPrimaryKeyExists(string tableName, string primaryKeyColumn, string keyValue)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                $"SELECT COUNT(*) FROM `{tableName}` WHERE `{primaryKeyColumn}` = @val;", conn);
            cmd.Parameters.AddWithValue("@val", keyValue);
            return (long) (cmd.ExecuteScalar() ?? 0) > 0;
        }

        public void InsertRow(string tableName, Dictionary<string, object> rowData)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cols = string.Join(", ", rowData.Keys.Select(k => $"`{k}`"));
            var vals = string.Join(", ", rowData.Keys.Select(k => $"@{k}"));
            using var cmd = new MySqlCommand(
                $"INSERT INTO `{tableName}` ({cols}) VALUES ({vals});", conn);
            foreach (var kv in rowData)
                cmd.Parameters.AddWithValue($"@{kv.Key}", kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName,
            object newValue)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var pk = GetPrimaryKeyColumn(tableName);
            if (pk == null || !rowData.ContainsKey(pk))
            {
                Debug.LogError(
                    $"[ERROR] Cannot update '{columnName}' in '{tableName}': No primary key found or its value is missing.");
                return;
            }

            object valueToDb;

            switch (newValue)
            {
                case Vector2 v2:
                    valueToDb = JsonConvert.SerializeObject(new {x = v2.x, y = v2.y});
                    break;
                case Vector3 v3:
                    valueToDb = JsonConvert.SerializeObject(new {x = v3.x, y = v3.y, z = v3.z});
                    break;

                case Sprite sprite:
                    valueToDb = GetAssetPath(sprite);
                    break;
                case GameObject go:
                    valueToDb = GetAssetPath(go);
                    break;

                default:
                    valueToDb = newValue;
                    break;
            }

            try
            {
                using var cmd = new MySqlCommand(
                    $"UPDATE `{tableName}` SET `{columnName}` = @newValue WHERE `{pk}` = @primaryKeyValue;", conn);

                cmd.Parameters.AddWithValue("@newValue", valueToDb);
                cmd.Parameters.AddWithValue("@primaryKeyValue", rowData[pk]);

                int rowsAffected = cmd.ExecuteNonQuery();
                Debug.Log($"[Database] Update successful. Rows affected: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Database] Error updating cell value: {ex.Message}");
            }
        }

        public void DeleteRow(string tableName, Dictionary<string, object> rowData)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var pk = GetPrimaryKeyColumn(tableName);
            string query;
            var pars = new List<MySqlParameter>();

            if (pk != null && rowData.ContainsKey(pk))
            {
                query = $"DELETE FROM `{tableName}` WHERE `{pk}` = @pk;";
                pars.Add(new MySqlParameter("@pk", rowData[pk]));
            }
            else
            {
                var conds = rowData
                    .Where(kv => kv.Value != null)
                    .Select(kv => $"`{kv.Key}` = @{kv.Key}");
                query = $"DELETE FROM `{tableName}` WHERE {string.Join(" AND ", conds)};";
                pars = rowData
                    .Where(kv => kv.Value != null)
                    .Select(kv => new MySqlParameter($"@{kv.Key}", kv.Value))
                    .ToList();
            }

            using var cmd = new MySqlCommand(query, conn);
            pars.ForEach(p => cmd.Parameters.Add(p));
            cmd.ExecuteNonQuery();
        }

        public void AddColumn(string tableName, string columnName, string columnType)
        {
            var type = columnType;
            if (type == "Vector2" || type == "Vector3" || type == "GameObject" || type == "Sprite")
                type = "TEXT";
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {type};", conn);
            cmd.ExecuteNonQuery();
        }

        public void ModifyColumn(string tableName, string oldName, string newName, string newType, bool isPrimaryKey)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            string existingPrimaryKeyColumn = null;
            using (var cmd = new MySqlCommand($"SHOW KEYS FROM `{tableName}` WHERE Key_name = 'PRIMARY'", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        existingPrimaryKeyColumn = reader.GetString(4);
                    }
                }
            }

            string commandText = $"ALTER TABLE `{tableName}` CHANGE COLUMN `{oldName}` `{newName}` {newType}";

            using var transaction = conn.BeginTransaction();

            if (isPrimaryKey && oldName != existingPrimaryKeyColumn)
            {
                if (!string.IsNullOrEmpty(existingPrimaryKeyColumn))
                {
                    using var dropPkCmd =
                        new MySqlCommand($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;", conn, transaction);
                    dropPkCmd.ExecuteNonQuery();
                }

                commandText += " PRIMARY KEY";
            }

            using (var modifyCmd = new MySqlCommand(commandText + ";", conn, transaction))
            {
                modifyCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public void DeleteColumn(string tableName, string columnName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                $"ALTER TABLE `{tableName}` DROP COLUMN `{columnName}`;", conn);
            cmd.ExecuteNonQuery();
        }

        public void ChangeColumnType(string tableName, string oldColumnName, string newColumnName, string newColumnType)
        {
        }

        public void MakePrimaryKey(string tableName, string newPrimaryKey)
        {
            try
            {
                var columns = GetTableColumns(tableName);
                var columnToModify =
                    columns.FirstOrDefault(c => c.Name.Equals(newPrimaryKey, StringComparison.OrdinalIgnoreCase));

                if (columnToModify != null)
                    ModifyColumn(tableName, newPrimaryKey, newPrimaryKey, columnToModify.Type, true);
                else
                    Debug.LogError(
                        $"[MySQL Provider] Column '{newPrimaryKey}' not found in table '{tableName}'. Cannot make it a primary key.");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[MySQL Provider] Failed to make column '{newPrimaryKey}' a primary key in table '{tableName}'. Error: {e.Message}");
            }
        }

        public bool IsAutoIncrement(string tableName, string columnName)
        {
            if (string.IsNullOrEmpty(_databaseName))
            {
                Debug.LogError("Database name is not set, cannot check for auto_increment in MySQL.");
                return false;
            }

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS " +
                                      "WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = @tableName AND COLUMN_NAME = @colName";

                    cmd.Parameters.AddWithValue("@dbName", _databaseName);
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    cmd.Parameters.AddWithValue("@colName", columnName);

                    var result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        return result.ToString().Contains("auto_increment");
                    }
                }
            }

            return false;
        }
    }
}