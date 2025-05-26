using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using Nhance.UnityDatabaseTool.Data;
using Unity.VisualScripting;
using UnityEngine;

namespace Nhance.UnityDatabaseTool.DatabaseProviders
{
    public class MySqlProvider : IDatabaseProvider
    {
        private MySqlConnection _connection;
        private readonly string _connectionString;

        private List<Table> _tables = new();
        public MySqlProvider(string connectionString) => _connectionString = connectionString;

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
            if (_connectionString != null)
                return;

            _connection = new MySqlConnection(_connectionString);
            _connection.Open();
            
            if (_tables.Count > 0)
                LoadTables(_tables);
        }
        
        public void CloseConnection()
        {
            if (_connectionString == null)
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
            SELECT COLUMN_NAME, DATA_TYPE, COLUMN_KEY
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION;", conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cols.Add(new Database.TableColumn
                {
                    Name = rdr.GetString("COLUMN_NAME"),
                    Type = rdr.GetString("DATA_TYPE").ToUpper(),
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
            string newValue)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var pk = GetPrimaryKeyColumn(tableName);
            if (pk == null || !rowData.ContainsKey(pk))
            {
                Debug.LogError($"[ERROR] Cannot update '{columnName}' in '{tableName}': No primary key found.");
                return;
            }

            object converted = newValue;
            var orig = rowData[columnName];
            if (orig is long) converted = long.TryParse(newValue, out var lv) ? lv : newValue;
            else if (orig is int) converted = int.TryParse(newValue, out var iv) ? iv : newValue;
            else if (orig is double) converted = double.TryParse(newValue, out var dv) ? dv : newValue;

            using var cmd = new MySqlCommand(
                $"UPDATE `{tableName}` SET `{columnName}` = @new WHERE `{pk}` = @pk;", conn);
            cmd.Parameters.AddWithValue("@new", converted);
            cmd.Parameters.AddWithValue("@pk", rowData[pk]);
            cmd.ExecuteNonQuery();
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

        public void ModifyColumn(string tableName, int columnIndex, string newName, string newType, bool isPrimaryKey)
        {
            var old = GetColumnNames(tableName)[columnIndex];
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            if (isPrimaryKey)
            {
                using var drop = new MySqlCommand($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;", conn);
                drop.ExecuteNonQuery();
            }

            using var cmd = new MySqlCommand(
                $"ALTER TABLE `{tableName}` CHANGE COLUMN `{old}` `{newName}` {newType}"
                + (isPrimaryKey ? " PRIMARY KEY" : "") + ";", conn);
            cmd.ExecuteNonQuery();
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
        }
    }
}