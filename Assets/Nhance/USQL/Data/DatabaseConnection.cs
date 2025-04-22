using System.Collections.Generic;
using MySqlConnector;
using UnityEngine;

public class DatabaseConnection
{
    public string Name;
    public string Path; // тут будет строка подключения
    public bool ShowDatabases;
    public List<Database> Databases;

    public DatabaseConnection(string connectionString)
    {
        Path = connectionString;
        Name = "MySQL_Connection";
        Databases = new List<Database>();
        RefreshDatabases();
    }

    public void RefreshDatabases()
    {
        Databases.Clear();

        using (var connection = new MySqlConnection(Path))
        {
            connection.Open();
            using (var command = new MySqlCommand("SHOW DATABASES;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string dbName = reader.GetString(0);
                    Databases.Add(new Database(dbName, Path));
                }
            }
        }
    }

    public void CreateDatabase(string databaseName)
    {
        using (var connection = new MySqlConnection(Path))
        {
            connection.Open();
            using (var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{databaseName}`;", connection))
            {
                command.ExecuteNonQuery();
                Databases.Add(new Database(databaseName, Path));
                Debug.Log($"[INFO] Database '{databaseName}' создана.");
            }
        }
    }
}