using System.Collections.Generic;
using MySqlConnector;
using UnityEngine;

public class DatabaseConnection
{
    public string Name;
    public string Path; // тут будет строка подключения
    public bool ShowDatabases;
    public List<Database> Databases;

    public EConnectionType ConnectionType;

    
    public DatabaseConnection(string connectionString, EConnectionType type)
    {
        Path = connectionString;
        ConnectionType = type;
        Name = type == EConnectionType.MySQL ? "MySQL_Connection" : "SQLite_Connection";
        Databases = new List<Database>();
        RefreshDatabases();
    }


    public void RefreshDatabases()
    {
        Databases.Clear();

        if (ConnectionType == EConnectionType.MySQL)
        {
            using (var connection = new MySqlConnection(Path))
            {
                connection.Open();
                using (var command = new MySqlCommand("SHOW DATABASES;", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string dbName = reader.GetString(0);
                        Databases.Add(new Database(dbName, Path, EConnectionType.MySQL));
                    }
                }
            }
        }
        else // SQLite
        {
            // Для SQLite создаём один "виртуальный" Database, так как база — это файл
            Databases.Add(new Database("SQLite_DB", Path, EConnectionType.SQLite));
        }
    }


    public void CreateDatabase(string databaseName)
    {
        if (ConnectionType == EConnectionType.MySQL)
        {
            using (var connection = new MySqlConnection(Path))
            {
                connection.Open();
                using (var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{databaseName}`;", connection))
                {
                    command.ExecuteNonQuery();
                    Databases.Add(new Database(databaseName, Path, EConnectionType.MySQL));
                    Debug.Log($"[INFO] Database '{databaseName}' создана.");
                }
            }
        }
        else
        {
            Debug.LogWarning("[INFO] SQLite не поддерживает создание нескольких баз в одном файле.");
        }
    }

    
    public enum EConnectionType
    {
        MySQL,
        SQLite
    }

}