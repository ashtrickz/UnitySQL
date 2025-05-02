using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using UnityEngine;
using MySqlConnector;
using UnityEditor;

public class Table
{
    public string Name;
    public List<Dictionary<string, object>> Data = new List<Dictionary<string, object>>();

    public Table(string name)
    {
        Name = name;
    }

    public void LoadContent(Database database)
    {
        Data.Clear();
        switch (database.ConnectionType)
        {
            case DatabaseConnection.EConnectionType.SQLite:
                using (var connection = new SqliteConnection($"Data Source={database.ConnectionString};Version=3;"))
                {
                    connection.Open();

                    using (var command = new SqliteCommand($"SELECT * FROM {Name};", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string columnName = reader.GetName(i);
                                object value = reader.GetValue(i);

                                if (value is DBNull) value = null;

                                string columnType = database.GetColumnType_Lite(Name, columnName);

                                try
                                {
                                    // **Retrieve GameObject from Asset Path**
                                    if (columnType == "GameObject" && value is string assetPath)
                                    {
                                        row[columnName] = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                                    }
                                    // **Retrieve Sprite from Asset Path**
                                    else if (columnType == "Sprite" && value is string spritePath)
                                    {
                                        row[columnName] = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                                    }
                                    // **Parse Vector2**
                                    else if (columnType == "Vector2" && value is string vector2Str)
                                    {
                                        row[columnName] = ParseVector2(vector2Str);
                                    }
                                    // **Parse Vector3**
                                    else if (columnType == "Vector3" && value is string vector3Str)
                                    {
                                        row[columnName] = ParseVector3(vector3Str);
                                    }
                                    else
                                    {
                                        row[columnName] = value;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError(
                                        $"[ERROR] Failed to parse column '{columnName}' in table '{Name}': {e.Message}");
                                    row[columnName] = value; // Keep raw value to prevent crashes
                                }
                            }

                            Data.Add(row);
                        }
                    }
                }

                break;
            case DatabaseConnection.EConnectionType.MySQL:
                using (var connection = new MySqlConnection(database.ConnectionString))
                {
                    connection.Open();
                    using (var command = new MySqlCommand($"SELECT * FROM `{Name}`;", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var columnName = reader.GetName(i);
                                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                row[columnName] = value;
                            }

                            Data.Add(row);
                        }
                    }
                }

                break;
        }
    }


    private Vector2 ParseVector2(string value)
    {
        try
        {
            string[] parts = value.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
            {
                return new Vector2(x, y);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ERROR] Failed to parse Vector2: '{value}' - {e.Message}");
        }

        return Vector2.zero; // Return default instead of breaking the table
    }

    private Vector3 ParseVector3(string value)
    {
        try
        {
            string[] parts = value.Split(',');
            if (parts.Length == 3 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                return new Vector3(x, y, z);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ERROR] Failed to parse Vector3: '{value}' - {e.Message}");
        }

        return Vector3.zero; // Return default instead of breaking the table
    }

    public void ClearTable(Database database)
    {
        using (var connection = new MySqlConnection(database.ConnectionString))
        {
            connection.Open();
            using (var command = new MySqlCommand($"TRUNCATE TABLE `{Name}`;", connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public void InsertData(Dictionary<string, object> rowData, Database database)
    {
        using (var connection = new MySqlConnection(database.ConnectionString))
        {
            connection.Open();

            string columns = string.Join(", ", rowData.Keys);
            string parameters = string.Join(", ", rowData.Keys);

            string query =
                $"INSERT INTO `{Name}` ({columns}) VALUES ({string.Join(", ", rowData.Keys.Select(k => "@" + k))});";

            using (var command = new MySqlCommand(query, connection))
            {
                foreach (var pair in rowData)
                {
                    command.Parameters.AddWithValue("@" + pair.Key, pair.Value);
                }

                command.ExecuteNonQuery();
            }
        }
    }
}