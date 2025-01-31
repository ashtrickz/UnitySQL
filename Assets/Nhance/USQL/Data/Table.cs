using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
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

    using (var connection = new Mono.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Version=3;"))
    {
        connection.Open();

        using (var command = new Mono.Data.Sqlite.SqliteCommand($"SELECT * FROM {Name};", connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object value = reader.GetValue(i);

                    if (value is string strValue)
                    {
                        if (strValue.Contains(",") && strValue.Split(',').Length == 2)
                        {
                            string[] parts = strValue.Split(',');
                            row[columnName] = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                        }
                        else if (strValue.Contains(",") && strValue.Split(',').Length == 3)
                        {
                            string[] parts = strValue.Split(',');
                            row[columnName] = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                        }
                        else
                        {
                            row[columnName] = strValue;
                        }
                    }
                    else if (value is byte[] imageData)
                    {
                        Texture2D texture = new Texture2D(2, 2);
                        texture.LoadImage(imageData);
                        row[columnName] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
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