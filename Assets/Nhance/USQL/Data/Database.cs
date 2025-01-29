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
                columns.Add($"{columnNames[i]} {columnTypes[i]}");
            }

            string query = $"CREATE TABLE {tableName} ({string.Join(", ", columns)});";
            using (var command = new Mono.Data.Sqlite.SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            Debug.Log($"[SUCCESS] Table '{tableName}' created successfully.");
            RefreshTables();
        }
    }

    
    public void InsertIntoTable(string tableName, Dictionary<string, object> rowData)
    {
        var table = Tables.FirstOrDefault(t => t.Name == tableName);
        if (table != null)
        {
            table.InsertData(rowData, Path);
        }
    }
    
    public void AddColumnToTable(string tableName, string columnName, string columnType)
    {
        using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path};Version=3;"))
        {
            connection.Open();

            string query = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";

            using (var command = new Mono.Data.Sqlite.SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }
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
            table.LoadContent(Path);
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

        string tempTableName = tableName + "_temp";

        // Get all columns except the one to be removed
        List<string> columns = new List<string>();

        using (var command = new Mono.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string colName = reader.GetString(1);
                if (colName != columnName) // Exclude column to be removed
                {
                    columns.Add(colName);
                }
            }
        }

        if (columns.Count == 0)
        {
            Debug.LogError($"[ERROR] Cannot remove all columns from {tableName}.");
            return;
        }

        string columnsList = string.Join(", ", columns);

        // Ensure the temp table does not already exist
        using (var checkCommand = new Mono.Data.Sqlite.SqliteCommand($"DROP TABLE IF EXISTS {tempTableName};", connection))
        {
            checkCommand.ExecuteNonQuery();
        }

        // Create new table without the unwanted column
        string createQuery = $"CREATE TABLE {tempTableName} AS SELECT {columnsList} FROM {tableName};";
        using (var createCommand = new Mono.Data.Sqlite.SqliteCommand(createQuery, connection))
        {
            createCommand.ExecuteNonQuery();
        }

        // Drop the old table and rename the new one
        using (var dropCommand = new Mono.Data.Sqlite.SqliteCommand($"DROP TABLE {tableName};", connection))
        {
            dropCommand.ExecuteNonQuery();
        }

        using (var renameCommand = new Mono.Data.Sqlite.SqliteCommand($"ALTER TABLE {tempTableName} RENAME TO {tableName};", connection))
        {
            renameCommand.ExecuteNonQuery();
        }

        Debug.Log($"[SUCCESS] Column '{columnName}' removed from '{tableName}'.");
    }
}

    
    public void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName, string newValue)
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

        string updateQuery = $"UPDATE {tableName} SET {columnName} = @NewValue WHERE {primaryKeyColumn} = @PrimaryKeyValue";

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
                Debug.LogError($"[ERROR] Cannot delete row from {tableName} because no valid column values were provided.");
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
                Debug.LogWarning($"[WARNING] No matching row found for deletion in {tableName}. It might have already been deleted.");
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
        string duplicateCheckQuery = $"SELECT {newPrimaryKey}, COUNT(*) FROM {tableName} GROUP BY {newPrimaryKey} HAVING COUNT(*) > 1";
        using (var duplicateCommand = new Mono.Data.Sqlite.SqliteCommand(duplicateCheckQuery, connection))
        using (var reader = duplicateCommand.ExecuteReader())
        {
            if (reader.HasRows)
            {
                Debug.LogError($"[ERROR] Cannot set '{newPrimaryKey}' as PRIMARY KEY in '{tableName}' because it has duplicate values.");
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

    public void RemoveTable(Table table)
    {
        // Logic for removing a table
    }

    public void ExecuteSQLQuery()
    {
        // Execute SQL Query logic
    }
}