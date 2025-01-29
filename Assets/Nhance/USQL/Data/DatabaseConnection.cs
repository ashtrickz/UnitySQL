using System.Collections.Generic;

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
}