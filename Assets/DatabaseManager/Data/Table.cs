using System;
using System.Collections.Generic;
using System.Data.Common;
using MySqlConnector;
using UnityEditor;
using UnityEngine;

namespace DatabaseManager.Data
{
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
            
            DbConnection connection = null;
            DbCommand command = null;

            try
            {
                switch (database.ConnectionType)
                {
                    case DatabaseConnection.EConnectionType.SQLite:
                        connection = new SqliteConnection($"Data Source={database.ConnectionString};Version=3;");
                        command = new SqliteCommand($"SELECT * FROM {Name};", (SqliteConnection)connection);
                        break;
                    case DatabaseConnection.EConnectionType.MySQL:
                        connection = new MySqlConnection(database.ConnectionString);
                        command = new MySqlCommand($"SELECT * FROM `{Name}`;", (MySqlConnection)connection);
                        break;
                }

                if (connection == null) return;
                
                connection.Open();
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            
                            string columnType = database.GetColumnType(Name, columnName);
                            object value;
                            
                            try
                            {
                                if (columnType == "GameObject" || columnType == "Sprite" || columnType == "Vector2" || columnType == "Vector3" || columnType == "DATE" || columnType == "DATETIME")
                                {
                                    value = reader.IsDBNull(i) ? null : reader.GetString(i);
                                }
                                else
                                {
                                    value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                
                                if (value is DBNull)
                                {
                                    row[columnName] = null;
                                }
                                else if (columnType == "GameObject" && value is string assetPath)
                                {
                                    row[columnName] = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                                }
                                else if (columnType == "Sprite" && value is string spritePath)
                                {
                                    row[columnName] = string.IsNullOrEmpty(spritePath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                                }
                                else if (columnType == "Vector2" && value is string vector2Str)
                                {
                                    row[columnName] = ParseVector2(vector2Str);
                                }
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
                                Debug.LogError($"[ERROR] Failed to parse column '{columnName}' in table '{Name}': {e.Message}. Falling back to raw value.");
                                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                        }
                        Data.Add(row);
    
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FATAL ERROR] Failed to load content for table '{Name}': {ex.Message}");
            }
            finally
            {
                connection?.Close();
                command?.Dispose();
            }
        }
    
        private Vector2 ParseVector2(string value)
        {
            if (string.IsNullOrEmpty(value)) return Vector2.zero;
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

            return Vector2.zero;
        }

        private Vector3 ParseVector3(string value)
        {
            if (string.IsNullOrEmpty(value)) return Vector3.zero;
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

            return Vector3.zero;
        }
    }
}