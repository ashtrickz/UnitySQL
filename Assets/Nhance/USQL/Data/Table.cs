using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;

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
                        object value = reader.GetValue(i);

                        // Explicitly store numeric values in the correct type
                        if (value is Int64 int64Value)
                        {
                            row[reader.GetName(i)] = int64Value; // Keep it as Int64 (long)
                        }
                        else if (value is Int32 int32Value)
                        {
                            row[reader.GetName(i)] = int32Value; // Keep as Int32
                        }
                        else if (value is double doubleValue)
                        {
                            row[reader.GetName(i)] = doubleValue; // Store as double
                        }
                        else if (value is float floatValue)
                        {
                            row[reader.GetName(i)] = floatValue; // Store as float
                        }
                        else
                        {
                            row[reader.GetName(i)] = value?.ToString(); // Convert everything else to string
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