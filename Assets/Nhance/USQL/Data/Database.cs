using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using UnityEngine;
using EConnectionType = DatabaseConnection.EConnectionType;

public class Database
{
    public string Name;
    public string ConnectionString;
    public List<Table> Tables;
    public string SQLQuery;

    public EConnectionType ConnectionType;

    
    public Database(string name, string connectionString, EConnectionType type)
    {
        Name = name;
        ConnectionString = type == EConnectionType.MySQL ? connectionString + $"Database={name};" : connectionString;
        ConnectionType = type;
        Tables = new List<Table>();
        if (type == EConnectionType.MySQL) LoadTables_Maria();
        else LoadTables_Lite();
    }

    public List<string> GetTableNames()
    {
        List<string> tableNames = new List<string>();
        foreach (var table in Tables)
            tableNames.Add(table.Name);
        return tableNames;
    }


    public void RefreshTables()
    {
        Tables.Clear();

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            using (var command = new MySqlCommand("SHOW TABLES;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Tables.Add(new Table(reader.GetString(0)));
                }
            }
        }
    }

    public void CreateTable_Lite(string tableName, List<ColumnDefinition> columns, int primaryKeyIndex)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            List<string> columnDefinitions = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                string columnType = columns[i].Type;

                // **Store Vector2, Vector3, GameObject, and Sprite as TEXT**
                if (columnType == "Vector2" || columnType == "Vector3" || columnType == "GameObject" ||
                    columnType == "Sprite")
                {
                    columnType = "TEXT";
                }

                if (i == primaryKeyIndex)
                {
                    columnDefinitions.Add($"{columns[i].Name} {columnType} PRIMARY KEY");
                }
                else
                {
                    columnDefinitions.Add($"{columns[i].Name} {columnType}");
                }
            }

            string query = $"CREATE TABLE {tableName} ({string.Join(", ", columnDefinitions)});";
            using (var command = new SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log($"[INFO] Table '{tableName}' created successfully.");
        }

        LoadTables_Lite(); // Refresh table list
    }

    public void CreateTable_Maria(string tableName, List<ColumnDefinition> columns, int primaryKeyIndex)
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            List<string> columnDefinitions = new List<string>();
            string primaryKeyName = null;

            for (int i = 0; i < columns.Count; i++)
            {
                string columnType = columns[i].Type;

                // MySQL типы данных — здесь ты можешь адаптировать под свои нужды
                if (columnType == "Vector2" || columnType == "Vector3" || columnType == "GameObject" ||
                    columnType == "Sprite")
                {
                    columnType = "TEXT";
                }

                string columnDef = $"`{columns[i].Name}` {columnType}";
                columnDefinitions.Add(columnDef);

                if (i == primaryKeyIndex)
                {
                    primaryKeyName = columns[i].Name;
                }
            }

            if (!string.IsNullOrEmpty(primaryKeyName))
            {
                columnDefinitions.Add($"PRIMARY KEY(`{primaryKeyName}`)");
            }

            string query = $"CREATE TABLE `{tableName}` ({string.Join(", ", columnDefinitions)});";

            using (var command = new MySqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log($"[INFO] Table `{tableName}` created successfully.");
        }

        LoadTables_Maria(); // Обновление списка таблиц
    }

    public void LoadTables_Lite()
    {
        Tables.Clear();

        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            using (var command = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table';", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Tables.Add(new Table(reader.GetString(0)));
                }
            }
        }
    }

    public void LoadTables_Maria()
    {
        Tables.Clear();

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            using (var command = new MySqlCommand("SHOW TABLES;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Tables.Add(new Table(reader.GetString(0)));
                }
            }
        }
    }

    public void ModifyColumn_Lite(string tableName, int columnIndex, string newName, string newType, bool isPrimaryKey)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                // SQLite does not support ALTER COLUMN directly, so we must recreate the table
                command.CommandText =
                    $"ALTER TABLE {tableName} RENAME COLUMN {GetColumnNames_Maria(tableName)[columnIndex]} TO {newName};";
                command.ExecuteNonQuery();
            }
        }
    }

    public void ModifyColumn_Maria(string tableName, int columnIndex, string newName, string newType, bool isPrimaryKey)
    {
        var oldColumnName = GetColumnNames_Maria(tableName)[columnIndex];

        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        // MySQL поддерживает изменение типа и имени колонки через CHANGE COLUMN
        string alterQuery = $"ALTER TABLE `{tableName}` CHANGE COLUMN `{oldColumnName}` `{newName}` {newType}";

        if (isPrimaryKey)
        {
            // Важно: сначала нужно убрать старый PRIMARY KEY, если он существует
            string pkColumn = GetPrimaryKeyColumn_Maria(tableName);
            if (!string.IsNullOrEmpty(pkColumn))
            {
                var dropPK =
                    new MySqlConnector.MySqlCommand($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;", connection);
                dropPK.ExecuteNonQuery();
            }

            alterQuery += " PRIMARY KEY";
        }

        var command = new MySqlConnector.MySqlCommand(alterQuery, connection);
        command.ExecuteNonQuery();

        connection.Close();
    }


    public void InsertIntoTable_Lite(string tableName, Dictionary<string, object> rowData)
    {
        var table = Tables.FirstOrDefault(t => t.Name == tableName);
        if (table != null)
        {
            // Convert custom types to storable formats
            Dictionary<string, object> serializedData = new Dictionary<string, object>();

            foreach (var entry in rowData)
            {
                if (entry.Value is Vector2 vector2)
                {
                    serializedData[entry.Key] = $"{vector2.x},{vector2.y}";
                }
                else if (entry.Value is Vector3 vector3)
                {
                    serializedData[entry.Key] = $"{vector3.x},{vector3.y},{vector3.z}";
                }
                else if (entry.Value is Sprite sprite)
                {
                    serializedData[entry.Key] = sprite.texture.EncodeToPNG(); // Convert to PNG bytes
                }
                else if (entry.Value is GameObject gameObject)
                {
                    serializedData[entry.Key] = JsonUtility.ToJson(new GameObjectData(gameObject));
                }
                else
                {
                    serializedData[entry.Key] = entry.Value;
                }
            }

            // table.InsertData(serializedData, ConnectionString);
        }
    }

    public void InsertIntoTable_Maria(string tableName, Dictionary<string, object> rowData)
    {
        var table = Tables.FirstOrDefault(t => t.Name == tableName);
        if (table == null)
        {
            UnityEngine.Debug.LogError($"[ERROR] Table '{tableName}' not found.");
            return;
        }

        Dictionary<string, object> serializedData = new Dictionary<string, object>();

        foreach (var entry in rowData)
        {
            if (entry.Value is UnityEngine.Vector2 vector2)
            {
                serializedData[entry.Key] = $"{vector2.x},{vector2.y}";
            }
            else if (entry.Value is UnityEngine.Vector3 vector3)
            {
                serializedData[entry.Key] = $"{vector3.x},{vector3.y},{vector3.z}";
            }
            else if (entry.Value is UnityEngine.Sprite sprite)
            {
                // В MySQL лучше сохранять путь или base64, не PNG-байты напрямую
                serializedData[entry.Key] = sprite.name;
            }
            else if (entry.Value is UnityEngine.GameObject gameObject)
            {
                serializedData[entry.Key] = UnityEngine.JsonUtility.ToJson(new GameObjectData(gameObject));
            }
            else
            {
                serializedData[entry.Key] = entry.Value;
            }
        }

        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string columnNames = string.Join(", ", serializedData.Keys.Select(k => $"`{k}`"));
        string valuePlaceholders = string.Join(", ", serializedData.Keys.Select(k => $"@{k}"));

        string query = $"INSERT INTO `{tableName}` ({columnNames}) VALUES ({valuePlaceholders});";

        var command = new MySqlConnector.MySqlCommand(query, connection);
        foreach (var pair in serializedData)
        {
            command.Parameters.AddWithValue($"@{pair.Key}", pair.Value ?? System.DBNull.Value);
        }

        command.ExecuteNonQuery();
        connection.Close();

        UnityEngine.Debug.Log($"[SUCCESS] Inserted row into `{tableName}`.");
    }


    public string GetColumnType_Lite(string tableName, string columnName)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            using (var command = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string colName = reader.GetString(1);
                    string colType = reader.GetString(2);

                    if (colName == columnName)
                    {
                        // **Check if stored as TEXT but is actually Vector2 or Vector3**
                        if (colType == "TEXT")
                        {
                            try
                            {
                                using (var typeCheckCmd =
                                       new SqliteCommand($"SELECT {columnName} FROM {tableName} LIMIT 1;", connection))
                                using (var typeCheckReader = typeCheckCmd.ExecuteReader())
                                {
                                    if (typeCheckReader.Read() && typeCheckReader[0] is string sampleValue)
                                    {
                                        if (sampleValue.Contains(","))
                                        {
                                            int parts = sampleValue.Split(',').Length;
                                            if (parts == 2) return "Vector2";
                                            if (parts == 3) return "Vector3";
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                Debug.LogWarning(
                                    $"[WARNING] Failed to determine type of column '{columnName}' in table '{tableName}'. Defaulting to TEXT.");
                            }
                        }

                        return colType; // Return detected column type
                    }
                }
            }
        }

        return "TEXT"; // Default to TEXT if not found
    }

    public string GetColumnType_Maria(string tableName, string columnName)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string colType = "TEXT"; // значение по умолчанию

        // Получаем тип из INFORMATION_SCHEMA
        var command = new MySqlConnector.MySqlCommand(@"
        SELECT DATA_TYPE 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName
        LIMIT 1;", connection);

        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);

        var reader = command.ExecuteReader();
        if (reader.Read())
        {
            colType = reader.GetString(0).ToUpper(); // Пример: TEXT, VARCHAR, INT
        }

        reader.Close();

        // Попытка определить Vector2 или Vector3 на основе содержимого
        if (colType == "TEXT" || colType == "VARCHAR")
        {
            try
            {
                var typeCheckCmd = new MySqlConnector.MySqlCommand(
                    $"SELECT `{columnName}` FROM `{tableName}` WHERE `{columnName}` IS NOT NULL LIMIT 1;",
                    connection);

                var typeCheckReader = typeCheckCmd.ExecuteReader();
                if (typeCheckReader.Read() && typeCheckReader[0] is string sampleValue)
                {
                    if (sampleValue.Contains(","))
                    {
                        int parts = sampleValue.Split(',').Length;
                        if (parts == 2) colType = "Vector2";
                        else if (parts == 3) colType = "Vector3";
                    }
                }

                typeCheckReader.Close();
            }
            catch
            {
                UnityEngine.Debug.LogWarning(
                    $"[WARNING] Could not infer vector type for column `{columnName}` in `{tableName}`.");
            }
        }

        connection.Close();
        return colType;
    }


    /*public void AddColumnToTable(string tableName, string columnName, string columnType)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            // **1. Add Column to Table**
            string query = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
            using (var command = new Mono.Data.Sqlite.SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log($"[SUCCESS] Column '{columnName}' ({columnType}) added to {tableName}");

            // **2. Populate Existing Rows with Default Values**
            string defaultValue = columnType.Contains("GameObject") || columnType.Contains("Sprite")
                ? null
                : GetDefaultValueForType(columnType);
            //if (defaultValue != null) // Only execute if default value is needed
            //{
            string updateQuery = $"UPDATE {tableName} SET {columnName} = {defaultValue};";
            using (var updateCommand = new Mono.Data.Sqlite.SqliteCommand(updateQuery, connection))
            {
                updateCommand.ExecuteNonQuery();
            }

            Debug.Log($"[SUCCESS] Default value '{defaultValue}' set for all rows in column '{columnName}'");
            //}
        }
    }*/

    private string GetDefaultValueForType(string columnType)
    {
        if (columnType.Contains("TEXT")) return "''"; // Empty string for text columns
        if (columnType.Contains("INTEGER")) return "0"; // Default integer
        if (columnType.Contains("REAL")) return "0.0"; // Default float
        if (columnType.Contains("BLOB")) return "NULL"; // Null for binary objects (like textures)

        // **Ensure `GameObject` and `Sprite` are NULL**
        if (columnType.Contains("GameObject") || columnType.Contains("Sprite"))
        {
            return null; // GameObject & Sprite should be NULL
        }

        return null; // No default needed
    }


    public List<string> GetColumnNames_Lite(string tableName)
    {
        List<string> columns = new List<string>();

        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();
            using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1)); // Get column name
                }
            }
        }

        return columns;
    }

    public List<string> GetColumnNames_Maria(string tableName)
    {
        List<string> columns = new List<string>();

        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        var command = new MySqlConnector.MySqlCommand(@"
        SELECT COLUMN_NAME 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
        ORDER BY ORDINAL_POSITION;", connection);

        command.Parameters.AddWithValue("@tableName", tableName);

        var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        reader.Close();
        connection.Close();

        return columns;
    }


    public void LoadTableContent(string tableName)
    {
        var table = Tables.FirstOrDefault(t => t.Name == tableName);
        if (table != null)
        {
            table.LoadContent(this);
        }
    }

    /*public void DuplicateRowInTable(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            // Get column names dynamically
            List<string> columnNames = GetColumnNames_Maria(tableName);

            // Remove the primary key column if it's auto-incrementing
            string primaryKey = GetPrimaryKeyColumn_Maria(tableName);
            if (primaryKey != null)
            {
                columnNames.Remove(primaryKey);
                rowData.Remove(primaryKey);
            }

            string columns = string.Join(", ", columnNames);
            string values = string.Join(", ", columnNames.Select(c => "@" + c));

            string query = $"INSERT INTO {tableName} ({columns}) VALUES ({values});";

            using (var command = new Mono.Data.Sqlite.SqliteCommand(query, connection))
            {
                foreach (var column in columnNames)
                {
                    command.Parameters.AddWithValue("@" + column, rowData[column]);
                }

                command.ExecuteNonQuery();
            }

            Debug.Log($"Duplicated row in {tableName}");
        }
    }*/

    public void ChangeColumnType_Lite(string tableName, string oldColumnName, string newColumnName,
        string newColumnType)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            List<string> columnNames = new List<string>();
            List<string> columnDefinitions = new List<string>();

            using (var command = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string colName = reader.GetString(1);
                    string colType = reader.GetString(2);

                    if (colName == oldColumnName)
                    {
                        columnNames.Add(newColumnName);
                        columnDefinitions.Add($"{newColumnName} {newColumnType}");
                    }
                    else
                    {
                        columnNames.Add(colName);
                        columnDefinitions.Add($"{colName} {colType}");
                    }
                }
            }

            string tempTableName = tableName + "_temp";
            string newTableDefinition = string.Join(", ", columnDefinitions);
            string columnList = string.Join(", ", columnNames);

            string createQuery = $"CREATE TABLE {tempTableName} ({newTableDefinition});";
            string copyDataQuery = $"INSERT INTO {tempTableName} SELECT {columnList} FROM {tableName};";
            string dropQuery = $"DROP TABLE {tableName};";
            string renameQuery = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";

            using (var command = new SqliteCommand(createQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SqliteCommand(copyDataQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SqliteCommand(dropQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SqliteCommand(renameQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log(
                $"Column '{oldColumnName}' changed to '{newColumnName}' with type '{newColumnType}' in table '{tableName}'.");
        }
    }

    public void ChangeColumnType_Maria(string tableName, string oldColumnName, string newColumnName,
        string newColumnType)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        // Получаем текущий тип столбца, если не хотим потерять свойства
        var getTypeCommand = new MySqlConnector.MySqlCommand(@"
        SELECT COLUMN_TYPE 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;", connection);

        getTypeCommand.Parameters.AddWithValue("@table", tableName);
        getTypeCommand.Parameters.AddWithValue("@column", oldColumnName);

        string currentType = null;
        var reader = getTypeCommand.ExecuteReader();
        if (reader.Read())
        {
            currentType = reader.GetString(0);
        }

        reader.Close();

        // Выполняем изменение столбца (MySQL поддерживает и переименование, и смену типа)
        string query = $"ALTER TABLE `{tableName}` CHANGE COLUMN `{oldColumnName}` `{newColumnName}` {newColumnType};";

        var command = new MySqlConnector.MySqlCommand(query, connection);
        command.ExecuteNonQuery();

        connection.Close();

        UnityEngine.Debug.Log(
            $"[SUCCESS] Column `{oldColumnName}` changed to `{newColumnName}` with type `{newColumnType}` in table `{tableName}`.");
    }


    public void RemoveColumnFromTable_Lite(string tableName, string columnName)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            List<string> columnNames = new List<string>();
            List<string> columnDefinitions = new List<string>();
            string primaryKeyColumn = null;

            // **Get Existing Table Schema**
            using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string colName = reader.GetString(1);
                    string colType = reader.GetString(2);
                    bool isPrimaryKey = reader.GetInt32(5) == 1; // Check if it's the primary key

                    if (colName == columnName) continue; // Skip the column being deleted

                    columnNames.Add(colName);
                    columnDefinitions.Add($"{colName} {colType}");

                    if (isPrimaryKey)
                    {
                        primaryKeyColumn = colName; // Store primary key column
                    }
                }
            }

            if (columnNames.Count == 0)
            {
                Debug.LogError("[ERROR] Cannot delete the only column in the table.");
                return;
            }

            // **Rebuild Table Without Deleted Column**
            string tempTableName = tableName + "_temp";
            string newTableDefinition = string.Join(", ", columnDefinitions);

            if (primaryKeyColumn != null)
            {
                newTableDefinition += $", PRIMARY KEY({primaryKeyColumn})"; // Preserve primary key
            }

            string columnList = string.Join(", ", columnNames);

            string createQuery = $"CREATE TABLE {tempTableName} ({newTableDefinition});";
            string copyDataQuery = $"INSERT INTO {tempTableName} SELECT {columnList} FROM {tableName};";
            string dropQuery = $"DROP TABLE {tableName};";
            string renameQuery = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";

            using (var command = new Mono.Data.Sqlite.SqliteCommand(createQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new Mono.Data.Sqlite.SqliteCommand(copyDataQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new Mono.Data.Sqlite.SqliteCommand(dropQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new Mono.Data.Sqlite.SqliteCommand(renameQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log($"Column '{columnName}' removed from '{tableName}', primary key preserved.");
        }
    }

    public void RemoveColumnFromTable_Maria(string tableName, string columnName)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        // Проверка: существует ли колонка
        var checkColumnCmd = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;", connection);

        checkColumnCmd.Parameters.AddWithValue("@table", tableName);
        checkColumnCmd.Parameters.AddWithValue("@column", columnName);

        long exists = (long) checkColumnCmd.ExecuteScalar();
        if (exists == 0)
        {
            UnityEngine.Debug.LogWarning($"[WARNING] Column '{columnName}' does not exist in table '{tableName}'.");
            connection.Close();
            return;
        }

        // Удаление колонки
        string dropColumnQuery = $"ALTER TABLE `{tableName}` DROP COLUMN `{columnName}`;";
        var dropCmd = new MySqlConnector.MySqlCommand(dropColumnQuery, connection);
        dropCmd.ExecuteNonQuery();

        connection.Close();

        UnityEngine.Debug.Log($"[SUCCESS] Column '{columnName}' removed from '{tableName}'.");
    }


    public void UpdateCellValue_Lite(string tableName, Dictionary<string, object> rowData, string columnName,
        string newValue)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            string primaryKeyColumn = GetPrimaryKeyColumn_Maria(tableName);
            if (primaryKeyColumn == null || !rowData.ContainsKey(primaryKeyColumn))
            {
                Debug.LogError($"[ERROR] Cannot update {columnName} in {tableName}: No primary key found.");
                return;
            }

            object primaryKeyValue = rowData[primaryKeyColumn];

            // Convert the new value based on its data type
            object convertedValue;
            if (rowData[columnName] is Int64)
            {
                if (long.TryParse(newValue, out long longValue))
                    convertedValue = longValue;
                else
                {
                    Debug.LogError($"[ERROR] Invalid Int64 value for column {columnName}");
                    return;
                }
            }
            else if (rowData[columnName] is Int32)
            {
                if (int.TryParse(newValue, out int intValue))
                    convertedValue = intValue;
                else
                {
                    Debug.LogError($"[ERROR] Invalid Int32 value for column {columnName}");
                    return;
                }
            }
            else if (rowData[columnName] is double)
            {
                if (double.TryParse(newValue, out double doubleValue))
                    convertedValue = doubleValue;
                else
                {
                    Debug.LogError($"[ERROR] Invalid double value for column {columnName}");
                    return;
                }
            }
            else
            {
                convertedValue = newValue; // Store as string if no numeric match
            }

            string updateQuery =
                $"UPDATE {tableName} SET {columnName} = @NewValue WHERE {primaryKeyColumn} = @PrimaryKeyValue";

            using (var command = new Mono.Data.Sqlite.SqliteCommand(updateQuery, connection))
            {
                command.Parameters.AddWithValue("@NewValue", convertedValue);
                command.Parameters.AddWithValue("@PrimaryKeyValue", primaryKeyValue);

                command.ExecuteNonQuery();
                Debug.Log($"[SUCCESS] Updated {columnName} in {tableName} to {newValue}");
            }
        }
    }

    public void UpdateCellValue_Maria(string tableName, Dictionary<string, object> rowData, string columnName,
        string newValue)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string primaryKeyColumn = GetPrimaryKeyColumn_Maria(tableName);
        if (primaryKeyColumn == null || !rowData.ContainsKey(primaryKeyColumn))
        {
            UnityEngine.Debug.LogError($"[ERROR] Cannot update `{columnName}` in `{tableName}`: No primary key found.");
            connection.Close();
            return;
        }

        object primaryKeyValue = rowData[primaryKeyColumn];

        object convertedValue;
        object originalValue = rowData[columnName];

        try
        {
            if (originalValue is long)
            {
                convertedValue = long.TryParse(newValue, out var val) ? val : throw new FormatException();
            }
            else if (originalValue is int)
            {
                convertedValue = int.TryParse(newValue, out var val) ? val : throw new FormatException();
            }
            else if (originalValue is double)
            {
                convertedValue = double.TryParse(newValue, out var val) ? val : throw new FormatException();
            }
            else
            {
                convertedValue = newValue; // сохраняем как строку
            }
        }
        catch
        {
            UnityEngine.Debug.LogError($"[ERROR] Invalid value '{newValue}' for column `{columnName}`.");
            connection.Close();
            return;
        }

        string updateQuery =
            $"UPDATE `{tableName}` SET `{columnName}` = @NewValue WHERE `{primaryKeyColumn}` = @PrimaryKeyValue;";

        var command = new MySqlConnector.MySqlCommand(updateQuery, connection);
        command.Parameters.AddWithValue("@NewValue", convertedValue);
        command.Parameters.AddWithValue("@PrimaryKeyValue", primaryKeyValue);

        int rowsAffected = command.ExecuteNonQuery();
        connection.Close();

        if (rowsAffected > 0)
        {
            UnityEngine.Debug.Log($"[SUCCESS] Updated `{columnName}` in `{tableName}` to '{newValue}'.");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[WARNING] No matching row found to update in `{tableName}`.");
        }
    }

    public void DeleteTable_Lite(string tableName)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            string query = $"DROP TABLE IF EXISTS {tableName};";
            using (var command = new SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log($"[INFO] Table '{tableName}' deleted.");
        }

        // **Remove from local list and refresh UI**
        Tables.RemoveAll(t => t.Name == tableName);
    }

    public void DeleteTable_Maria(string tableName)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string query = $"DROP TABLE IF EXISTS `{tableName}`;";
        var command = new MySqlConnector.MySqlCommand(query, connection);
        command.ExecuteNonQuery();

        connection.Close();

        UnityEngine.Debug.Log($"[INFO] Table '{tableName}' deleted.");

        // Удалить из локального списка и обновить UI
        Tables.RemoveAll(t => t.Name == tableName);
    }


    public string GetPrimaryKeyColumn_Lite(string tableName)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();
            using (var command = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int isPrimaryKey = reader.GetInt32(5); // 6th column is primary key flag
                    if (isPrimaryKey == 1)
                    {
                        return reader.GetString(1); // Return column name
                    }
                }
            }
        }

        return null; // No primary key found
    }

    public string GetPrimaryKeyColumn_Maria(string tableName)
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            using (var command = new MySqlCommand($@"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_KEY = 'PRI'
                LIMIT 1;", connection))
            {
                command.Parameters.AddWithValue("@tableName", tableName);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetString(0); // вернёт имя primary key колонки
                    }
                }
            }
        }

        return null; // если первичный ключ не найден
    }


    public void DeleteRowFromTable_Lite(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            string primaryKeyColumn = GetPrimaryKeyColumn_Maria(tableName);

            string query;
            List<string> conditions = new List<string>();
            List<SqliteParameter> parameters = new List<SqliteParameter>();

            if (primaryKeyColumn != null && rowData.ContainsKey(primaryKeyColumn))
            {
                query = $"DELETE FROM {tableName} WHERE {primaryKeyColumn} = @PrimaryKeyValue";
                parameters.Add(new SqliteParameter("@PrimaryKeyValue", rowData[primaryKeyColumn]));
            }
            else
            {
                foreach (var pair in rowData)
                {
                    if (pair.Value != null)
                    {
                        conditions.Add($"{pair.Key} = @{pair.Key}");
                        parameters.Add(new SqliteParameter($"@{pair.Key}", pair.Value));
                    }
                }

                if (conditions.Count == 0)
                {
                    Debug.LogError(
                        $"[ERROR] Cannot delete row from {tableName} because no valid column values were provided.");
                    return;
                }

                query = $"DELETE FROM {tableName} WHERE " + string.Join(" AND ", conditions);
            }

            using (var command = new SqliteCommand(query, connection))
            {
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }

                int affectedRows = command.ExecuteNonQuery();
                if (affectedRows > 0)
                {
                    Debug.Log($"[SUCCESS] Row deleted from {tableName}. Refreshing UI...");
                }
                else
                {
                    Debug.LogWarning(
                        $"[WARNING] No matching row found for deletion in {tableName}. It might have already been deleted.");
                }
            }
        }
    }

    public void DeleteRowFromTable_Maria(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            string primaryKeyColumn = GetPrimaryKeyColumn_Maria(tableName);

            string query;
            List<string> conditions = new List<string>();
            List<MySqlParameter> parameters = new List<MySqlParameter>();

            if (primaryKeyColumn != null && rowData.ContainsKey(primaryKeyColumn))
            {
                query = $"DELETE FROM `{tableName}` WHERE `{primaryKeyColumn}` = @PrimaryKeyValue";
                parameters.Add(new MySqlParameter("@PrimaryKeyValue", rowData[primaryKeyColumn]));
            }
            else
            {
                foreach (var pair in rowData)
                {
                    if (pair.Value != null)
                    {
                        string paramName = "@" + pair.Key;
                        conditions.Add($"`{pair.Key}` = {paramName}");
                        parameters.Add(new MySqlParameter(paramName, pair.Value));
                    }
                }

                if (conditions.Count == 0)
                {
                    Debug.LogError(
                        $"[ERROR] Cannot delete row from {tableName} because no valid column values were provided.");
                    return;
                }

                query = $"DELETE FROM `{tableName}` WHERE " + string.Join(" AND ", conditions);
            }

            using (var command = new MySqlCommand(query, connection))
            {
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }

                int affectedRows = command.ExecuteNonQuery();
                if (affectedRows > 0)
                {
                    Debug.Log($"[SUCCESS] Row deleted from `{tableName}`.");
                }
                else
                {
                    Debug.LogWarning($"[WARNING] No matching row found in `{tableName}` for deletion.");
                }
            }
        }
    }

    public void MakePrimaryKey_Lite(string tableName, string newPrimaryKey)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            // Check for duplicates in the column
            string duplicateCheckQuery =
                $"SELECT {newPrimaryKey}, COUNT(*) FROM {tableName} GROUP BY {newPrimaryKey} HAVING COUNT(*) > 1";
            using (var duplicateCommand = new Mono.Data.Sqlite.SqliteCommand(duplicateCheckQuery, connection))
            using (var reader = duplicateCommand.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    Debug.LogError(
                        $"[ERROR] Cannot set '{newPrimaryKey}' as PRIMARY KEY in '{tableName}' because it has duplicate values.");
                    return;
                }
            }

            // Get current column structure
            List<string> columns = new List<string>();
            List<string> columnDefinitions = new List<string>();
            string oldPrimaryKey = GetPrimaryKeyColumn_Maria(tableName);
            bool columnExists = false;

            using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string colName = reader.GetString(1);
                    string colType = reader.GetString(2);
                    columns.Add(colName);

                    if (colName == newPrimaryKey)
                    {
                        columnExists = true;
                        columnDefinitions.Add($"{colName} {colType} PRIMARY KEY"); // Set as PRIMARY KEY
                    }
                    else
                    {
                        columnDefinitions.Add($"{colName} {colType}");
                    }
                }
            }

            // If the column doesn't exist, return an error
            if (!columnExists)
            {
                Debug.LogError($"[ERROR] Column '{newPrimaryKey}' does not exist in table '{tableName}'.");
                return;
            }

            // Ensure no existing primary key conflicts
            if (oldPrimaryKey == newPrimaryKey)
            {
                Debug.LogWarning($"[INFO] Column '{newPrimaryKey}' is already the primary key.");
                return;
            }

            // Recreate the table with the new primary key
            string tempTableName = tableName + "_temp";
            string newTableDefinition = string.Join(", ", columnDefinitions);
            string columnsList = string.Join(", ", columns);

            string createQuery = $"CREATE TABLE {tempTableName} ({newTableDefinition});";
            string copyDataQuery = $"INSERT INTO {tempTableName} SELECT {columnsList} FROM {tableName};";
            string dropQuery = $"DROP TABLE {tableName};";
            string renameQuery = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";

            using (var createCommand = new Mono.Data.Sqlite.SqliteCommand(createQuery, connection))
            {
                createCommand.ExecuteNonQuery();
            }

            using (var copyCommand = new Mono.Data.Sqlite.SqliteCommand(copyDataQuery, connection))
            {
                try
                {
                    copyCommand.ExecuteNonQuery();
                }
                catch (Mono.Data.Sqlite.SqliteException ex)
                {
                    Debug.LogError($"[ERROR] Failed to copy data into new table: {ex.Message}");
                    return;
                }
            }

            using (var dropCommand = new Mono.Data.Sqlite.SqliteCommand(dropQuery, connection))
            {
                dropCommand.ExecuteNonQuery();
            }

            using (var renameCommand = new Mono.Data.Sqlite.SqliteCommand(renameQuery, connection))
            {
                renameCommand.ExecuteNonQuery();
            }

            Debug.Log($"[SUCCESS] Column '{newPrimaryKey}' is now the primary key for table '{tableName}'.");
        }
    }

    public void MakePrimaryKey_Maria(string tableName, string newPrimaryKey)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        // Проверим, существует ли указанная колонка
        var checkColumn = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;", connection);
        checkColumn.Parameters.AddWithValue("@table", tableName);
        checkColumn.Parameters.AddWithValue("@column", newPrimaryKey);

        long exists = (long) (checkColumn.ExecuteScalar() ?? 0);
        if (exists == 0)
        {
            UnityEngine.Debug.LogError($"[ERROR] Column '{newPrimaryKey}' does not exist in table '{tableName}'.");
            connection.Close();
            return;
        }

        // Проверим, уже ли это PRIMARY KEY
        string oldPrimaryKey = GetPrimaryKeyColumn_Maria(tableName);
        if (oldPrimaryKey == newPrimaryKey)
        {
            UnityEngine.Debug.LogWarning($"[INFO] Column '{newPrimaryKey}' is already the primary key.");
            connection.Close();
            return;
        }

        // Проверим на наличие дубликатов
        var checkDuplicates = new MySqlConnector.MySqlCommand($@"
        SELECT `{newPrimaryKey}`, COUNT(*) 
        FROM `{tableName}` 
        GROUP BY `{newPrimaryKey}` 
        HAVING COUNT(*) > 1;", connection);

        var reader = checkDuplicates.ExecuteReader();
        if (reader.HasRows)
        {
            reader.Close();
            UnityEngine.Debug.LogError($"[ERROR] Cannot set '{newPrimaryKey}' as PRIMARY KEY — duplicates found.");
            connection.Close();
            return;
        }

        reader.Close();

        // Удалим старый PRIMARY KEY
        if (!string.IsNullOrEmpty(oldPrimaryKey))
        {
            var dropPK = new MySqlConnector.MySqlCommand($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;", connection);
            dropPK.ExecuteNonQuery();
        }

        // Установим новый PRIMARY KEY
        var addPK = new MySqlConnector.MySqlCommand($"ALTER TABLE `{tableName}` ADD PRIMARY KEY (`{newPrimaryKey}`);",
            connection);
        addPK.ExecuteNonQuery();

        connection.Close();
        UnityEngine.Debug.Log($"[SUCCESS] Column '{newPrimaryKey}' is now the PRIMARY KEY for table '{tableName}'.");
    }


    public void RemoveTable(string tableName)
    {
        // Logic for removing a table
    }

    public void ExecuteSQLQuery()
    {
        // Execute SQL Query logic
    }

    public bool CheckPrimaryKeyExists_Lite(string tableName, string primaryKeyColumn, string keyValue)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            string query = $"SELECT COUNT(*) FROM {tableName} WHERE {primaryKeyColumn} = @keyValue;";
            using (var command = new SqliteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@keyValue", keyValue);
                long count = (long) command.ExecuteScalar();
                return count > 0;
            }
        }
    }

    public bool CheckPrimaryKeyExists_Maria(string tableName, string primaryKeyColumn, string keyValue)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string query = $"SELECT COUNT(*) FROM `{tableName}` WHERE `{primaryKeyColumn}` = @keyValue;";
        var command = new MySqlConnector.MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@keyValue", keyValue);

        long count = (long) (command.ExecuteScalar() ?? 0);
        connection.Close();

        return count > 0;
    }


    public void InsertRow_Lite(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            string columnNames = string.Join(", ", rowData.Keys);
            string placeholders = string.Join(", ", rowData.Keys.Select(k => $"@{k}"));

            string query = $"INSERT INTO {tableName} ({columnNames}) VALUES ({placeholders});";
            using (var command = new SqliteCommand(query, connection))
            {
                foreach (var kvp in rowData)
                {
                    command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                }

                command.ExecuteNonQuery();
            }
        }
    }

    public void InsertRow_Maria(string tableName, Dictionary<string, object> rowData)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string columnNames = string.Join(", ", rowData.Keys.Select(k => $"`{k}`"));
        string placeholders = string.Join(", ", rowData.Keys.Select(k => $"@{k}"));

        string query = $"INSERT INTO `{tableName}` ({columnNames}) VALUES ({placeholders});";
        var command = new MySqlConnector.MySqlCommand(query, connection);

        foreach (var kvp in rowData)
        {
            command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? System.DBNull.Value);
        }

        command.ExecuteNonQuery();
        connection.Close();
    }


    public void AddColumn_Lite(string tableName, string columnName, string columnType)
    {
        string sqlType = columnType;

        if (columnType == "Vector2" || columnType == "Vector3")
        {
            sqlType = "TEXT"; // Stored as "x,y" or "x,y,z"
        }

        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();

            string query = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {sqlType};";
            using (var command = new SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public void AddColumn_Maria(string tableName, string columnName, string columnType)
    {
        string sqlType = columnType;

        // Преобразование специфичных Unity-типов в SQL-совместимые
        if (columnType == "Vector2" || columnType == "Vector3" || columnType == "GameObject" || columnType == "Sprite")
        {
            sqlType = "TEXT"; // Сохраняем как строку
        }

        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        string query = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {sqlType};";
        var command = new MySqlConnector.MySqlCommand(query, connection);
        command.ExecuteNonQuery();

        connection.Close();
    }

    public List<TableColumn> GetTableColumns_Lite(string tableName)
    {
        List<TableColumn> columns = new List<TableColumn>();

        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({tableName});";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(new TableColumn
                        {
                            Name = reader["name"].ToString(),
                            Type = reader["type"].ToString(),
                            IsPrimaryKey = reader["pk"].ToString() == "1"
                        });
                    }
                }
            }
        }

        return columns;
    }

    public List<TableColumn> GetTableColumns_Maria(string tableName)
    {
        List<TableColumn> columns = new List<TableColumn>();

        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        var command = new MySqlConnector.MySqlCommand(@"
        SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_KEY
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
        ORDER BY ORDINAL_POSITION;", connection);

        command.Parameters.AddWithValue("@table", tableName);

        var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(new TableColumn
            {
                Name = reader.GetString("COLUMN_NAME"),
                Type = reader.GetString("COLUMN_TYPE"),
                IsPrimaryKey = reader.GetString("COLUMN_KEY") == "PRI"
            });
        }

        reader.Close();
        connection.Close();

        return columns;
    }


    public void DeleteColumn_Lite(string tableName, string columnName)
    {
        using (var connection = new SqliteConnection($"Data Source={ConnectionString};Version=3;"))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                // SQLite does not support direct column deletion, so we need to recreate the table
                command.CommandText = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
                command.ExecuteNonQuery();
            }
        }
    }

    public void DeleteColumn_Maria(string tableName, string columnName)
    {
        var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();

        // Проверим, существует ли колонка
        var checkCmd = new MySqlConnector.MySqlCommand(@"
        SELECT COUNT(*) 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;", connection);
        checkCmd.Parameters.AddWithValue("@table", tableName);
        checkCmd.Parameters.AddWithValue("@column", columnName);

        long exists = (long) (checkCmd.ExecuteScalar() ?? 0);
        if (exists == 0)
        {
            UnityEngine.Debug.LogWarning($"[WARNING] Column `{columnName}` does not exist in `{tableName}`.");
            connection.Close();
            return;
        }

        // Удаляем колонку
        string query = $"ALTER TABLE `{tableName}` DROP COLUMN `{columnName}`;";
        var command = new MySqlConnector.MySqlCommand(query, connection);
        command.ExecuteNonQuery();

        connection.Close();

        UnityEngine.Debug.Log($"[SUCCESS] Column `{columnName}` deleted from `{tableName}`.");
    }

    public class TableColumn
    {
        public string Name;
        public string Type;
        public bool IsPrimaryKey;
    }

    public class ColumnDefinition
    {
        public string Name;
        public string Type;
    }
}