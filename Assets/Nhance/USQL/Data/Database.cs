using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using UnityEngine;

public class Database
{
    public string Name;
    public string Path;
    public List<Table> Tables;
    public string SQLQuery;

    public Database(string name, string path)
    {
        Name = name;
        Path = path;
        Tables = new List<Table>();
        RefreshTables();
    }

    public void RefreshTables()
    {
        Tables.Clear();
        using (var connection = new SqliteConnection($"Data Source={Path};Version=3;"))
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

    public void AddNewTable()
    {
        // Logic for adding a new table
    }

    public void CreateTable(string tableName, List<string> columnNames, List<string> columnTypes)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            List<string> columns = new List<string>();
            for (int i = 0; i < columnNames.Count; i++)
            {
                string type = columnTypes[i];
                if (type == "Vector2" || type == "Vector3" || type == "GameObject") type = "TEXT";
                if (type == "Sprite") type = "BLOB";
                columns.Add($"{columnNames[i]} {type}");
            }

            string query = $"CREATE TABLE {tableName} ({string.Join(", ", columns)});";
            using (var command = new Mono.Data.Sqlite.SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public void InsertIntoTable(string tableName, Dictionary<string, object> rowData)
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

            table.InsertData(serializedData, Path);
        }
    }

    public string GetColumnType(string tableName, string columnName)
    {
        using (var connection = new SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            using (var command = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string colName = reader.GetString(1); // Column name
                    string colType = reader.GetString(2); // Column type

                    if (colName == columnName)
                    {
                        return colType; // Return the actual SQLite column type
                    }
                }
            }
        }

        return "TEXT"; // Default fallback type
    }


    public void AddColumnToTable(string tableName, string columnName, string columnType)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
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
    }

    public void ChangeColumnType(string tableName, string oldColumnName, string newColumnName, string newColumnType)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            List<string> columnNames = new List<string>();
            List<string> columnDefinitions = new List<string>();

            using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
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

            Debug.Log(
                $"Column '{oldColumnName}' changed to '{newColumnName}' with type '{newColumnType}' in table '{tableName}'.");
        }
    }


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


    public List<string> GetColumnNames(string tableName)
    {
        List<string> columns = new List<string>();

        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
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


    public void LoadTableContent(string tableName)
    {
        var table = Tables.FirstOrDefault(t => t.Name == tableName);
        if (table != null)
        {
            table.LoadContent(this);
        }
    }

    public void DuplicateRowInTable(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            // Get column names dynamically
            List<string> columnNames = GetColumnNames(tableName);

            // Remove the primary key column if it's auto-incrementing
            string primaryKey = GetPrimaryKeyColumn(tableName);
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
    }


  public void RemoveColumnFromTable(string tableName, string columnName)
{
    using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
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


    public void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName,
        string newValue)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            string primaryKeyColumn = GetPrimaryKeyColumn(tableName);
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


    public void RenameColumnInTable(string tableName, string oldColumnName, string newColumnName)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            // Get table structure
            using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
            using (var reader = command.ExecuteReader())
            {
                List<string> columns = new List<string>();
                List<string> newColumns = new List<string>();

                while (reader.Read())
                {
                    string colName = reader.GetString(1);
                    string colType = reader.GetString(2);

                    if (colName == oldColumnName)
                    {
                        newColumns.Add($"{newColumnName} {colType}"); // Use new column name
                        columns.Add(colName); // Keep old name for data transfer
                    }
                    else
                    {
                        newColumns.Add($"{colName} {colType}");
                        columns.Add(colName);
                    }
                }

                if (!columns.Contains(oldColumnName))
                {
                    Debug.LogError($"Column {oldColumnName} does not exist in table {tableName}.");
                    return;
                }

                string tempTableName = tableName + "_temp";
                string newTableDefinition = string.Join(", ", newColumns);
                string columnsList = string.Join(", ", columns);

                // Create new table
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
                    copyCommand.ExecuteNonQuery();
                }

                using (var dropCommand = new Mono.Data.Sqlite.SqliteCommand(dropQuery, connection))
                {
                    dropCommand.ExecuteNonQuery();
                }

                using (var renameCommand = new Mono.Data.Sqlite.SqliteCommand(renameQuery, connection))
                {
                    renameCommand.ExecuteNonQuery();
                }

                Debug.Log($"Column {oldColumnName} renamed to {newColumnName} in table {tableName}");
            }
        }
    }

    public string GetPrimaryKeyColumn(string tableName)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();
            using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
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


    public void DeleteRowFromTable(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            string primaryKeyColumn = GetPrimaryKeyColumn(tableName);

            string query;
            List<string> conditions = new List<string>();
            List<Mono.Data.Sqlite.SqliteParameter> parameters = new List<Mono.Data.Sqlite.SqliteParameter>();

            if (primaryKeyColumn != null && rowData.ContainsKey(primaryKeyColumn))
            {
                query = $"DELETE FROM {tableName} WHERE {primaryKeyColumn} = @PrimaryKeyValue";
                parameters.Add(new Mono.Data.Sqlite.SqliteParameter("@PrimaryKeyValue", rowData[primaryKeyColumn]));
            }
            else
            {
                foreach (var pair in rowData)
                {
                    if (pair.Value != null)
                    {
                        conditions.Add($"{pair.Key} = @{pair.Key}");
                        parameters.Add(new Mono.Data.Sqlite.SqliteParameter($"@{pair.Key}", pair.Value));
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

            using (var command = new Mono.Data.Sqlite.SqliteCommand(query, connection))
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


    public void MakePrimaryKey(string tableName, string newPrimaryKey)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
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
            string oldPrimaryKey = GetPrimaryKeyColumn(tableName);
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

    public void RemoveTable(string tableName)
    {
        // Logic for removing a table
    }

    public void ExecuteSQLQuery()
    {
        // Execute SQL Query logic
    }

    public bool CheckPrimaryKeyExists(string tableName, string primaryKeyColumn, string keyValue)
    {
        using (var connection = new SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            string query = $"SELECT COUNT(*) FROM {tableName} WHERE {primaryKeyColumn} = @keyValue;";
            using (var command = new SqliteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@keyValue", keyValue);
                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }
    }

    public void InsertRow(string tableName, Dictionary<string, object> rowData)
    {
        using (var connection = new SqliteConnection($"Data Source={Path};Version=3;"))
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

    public void AddColumn(string tableName, string columnName, string columnType)
    {
        using (var connection = new SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            string query = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
            using (var command = new SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

}