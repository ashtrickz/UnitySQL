using System.Collections.Generic;
using System.Linq;
using Nhance.UnityDatabaseTool.DatabaseProviders;
using UnityEditor;
using UnityEngine;
using EConnectionType = Nhance.UnityDatabaseTool.Data.DatabaseConnection.EConnectionType;

namespace Nhance.UnityDatabaseTool.Data
{
    public class Database
    {
        public string Name;
        public string ConnectionString;
        public List<Table> Tables;
        public string SQLQuery;

        public EConnectionType ConnectionType;

        private readonly IDatabaseProvider provider;

        public Database(string name, string connectionString, EConnectionType type)
        {
            Name = name;
            ConnectionType = type;
            ConnectionString = connectionString;
            Tables = new List<Table>();

            provider = type switch
            {
                EConnectionType.SQLite => new SqliteProvider(connectionString),
                EConnectionType.MySQL => new MySqlProvider(connectionString),
                _ => null
            };

            provider?.LoadTables(Tables);
        }

        public void LoadTableContent(string tableName)
            => Tables.FirstOrDefault(t => t.Name == tableName)?.LoadContent(this);

        public void CreateTable(string tableName, List<ColumnDefinition> columnDefinitions, int primaryKeyIndex)
            => provider.CreateTable(tableName, columnDefinitions, primaryKeyIndex);

        public List<string> GetTableNames()
            => Tables.Select(table => table.Name).ToList();

        public void ClearTable(string tableName)
            => provider.ClearTable(tableName);
        
        public void DeleteTable(string tableName)
            => provider.DeleteTable(tableName);

        public void AddColumn(string tableName, string columnName, string columnType)
            => provider.AddColumn(tableName, columnName, columnType);

        public List<TableColumn> GetTableColumns(string selectedTableForContent)
            => provider.GetTableColumns(selectedTableForContent);

        public void ModifyColumn(string tableName, string oldName, string newName, string newType, bool isPrimaryKey)
            => provider.ModifyColumn(tableName, oldName, newName, newType, isPrimaryKey);

        public string GetPrimaryKeyColumn(string selectedTableForContent)
            => provider.GetPrimaryKeyColumn(selectedTableForContent);

        public void MakePrimaryKeyColumn(string tableName, string columnName)
            => provider.MakePrimaryKey(tableName, columnName);

        public void DeleteColumn(string tableName, string columnName)
            => provider.DeleteColumn(tableName, columnName);

        public List<string> GetColumnNames(string table)
            => provider.GetColumnNames(table);

        public string GetColumnType(string selectedTableForContent, string column)
            => provider.GetColumnType(selectedTableForContent, column);

        public void InsertRow(string tableName, Dictionary<string, object> rowData)
            => provider.InsertRow(tableName, rowData);

        public void DeleteRowFromTable(string tableName, Dictionary<string, object> rowData)
            => provider.DeleteRow(tableName, rowData);

        public void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName,
            object newValue)
            => provider.UpdateCellValue(tableName, rowData, columnName, newValue);

        public void RefreshConnection() 
            => provider.RefreshConnection();
        
        public class TableColumn
        {
            public string Name;
            public string Type;
            public bool IsPrimaryKey;
        }

        public class ColumnDefinition
        {
            public string Name;
            public string Type;
            
            public int Length = 255; 
            
            public bool IsNotNull;
            public bool IsUnique;
        }

        public void RefreshTable(string selectedTableName)
        {
            LoadTableContent(selectedTableName);
        }

        public bool IsAutoIncrement(string tableName, string colName) 
            => provider.IsAutoIncrement(tableName, colName);
    }
}