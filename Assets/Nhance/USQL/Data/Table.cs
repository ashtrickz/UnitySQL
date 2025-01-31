using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using UnityEditor;
using UnityEngine;

public class Table
{
    public string Name;
    public List<Dictionary<string, object>> Data = new List<Dictionary<string, object>>(); // H

    public Table(string name)
    {
        Name = name;
    }

    public void LoadContent(string databasePath)
    {
        Data.Clear();

        using (var connection = new SqliteConnection($"Data Source={databasePath};Version=3;"))
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

                        if (value is string strValue && string.IsNullOrEmpty(strValue))
                        {
                            row[columnName] = null; // Convert empty strings to null
                        }
                        else if (columnName.Contains("GameObject"))
                        {
                            string prefabPath = value.ToString();
                            row[columnName] = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        }
                        else if (columnName.Contains("Sprite"))
                        {
                            string spritePath = value.ToString();
                            row[columnName] = string.IsNullOrEmpty(spritePath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                        }
                        else
                        {
                            row[columnName] = value;
                        }
                    }

                    Data.Add(row);
                }
            }
        }
    }


    public void InsertData(Dictionary<string, object> rowData, string databasePath)
    {
        using (var connection = new SqliteConnection($"Data Source={databasePath};Version=3;"))
        {
            connection.Open();
            
            string columns = string.Join(", ", rowData.Keys);
            string values = string.Join(", ", rowData.Keys.Select(k => "@" + k));

            string query = $"INSERT INTO {Name} ({columns}) VALUES ({values})";

            using (var command = new SqliteCommand(query, connection))
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