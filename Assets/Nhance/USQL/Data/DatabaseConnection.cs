using System.Collections.Generic;
using Mono.Data.Sqlite;
using UnityEngine;

public class DatabaseConnection
{
    public string Name;
    public string Path;
    public bool ShowDatabases;
    public List<Database> Databases;

    public DatabaseConnection(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        Databases = new List<Database> { new Database(Name, Path) };
    }
    
    public void CreateDatabase(string databaseName)
    {
        string databasePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), $"{databaseName}.db");

        if (!System.IO.File.Exists(databasePath))
        {
            SqliteConnection.CreateFile(databasePath);
            Database newDatabase = new Database(databaseName, databasePath);
            Databases.Add(newDatabase);
            Debug.Log($"[INFO] Database '{databaseName}' created at {databasePath}.");
        }
        else
        {
            Debug.LogError($"[ERROR] Database '{databaseName}' already exists.");
        }
    }

}