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

    public void LoadContent(Database database)
    {
        Data.Clear();

        using (var connection = new SqliteConnection($"Data Source={database.Path};Version=3;"))
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

                        // **Convert NULLs to Proper C# Values**
                        if (value is DBNull) value = null;

                        string columnType = database.GetColumnType(Name, columnName);

                        if (columnType == "GameObject" || columnType == "Sprite")
                        {
                            row[columnName] = value == null || string.IsNullOrEmpty(value.ToString()) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value.ToString());
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