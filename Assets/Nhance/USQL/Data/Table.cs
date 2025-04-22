using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MySqlConnector;

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

            string query = $"INSERT INTO `{Name}` ({columns}) VALUES ({string.Join(", ", rowData.Keys.Select(k => "@" + k))});";

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
