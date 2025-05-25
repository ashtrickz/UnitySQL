using System.Collections.Generic;
using Nhance.USQL.Data;

namespace Nhance.USQL.DatabaseProviders
{
    public interface IDatabaseProvider
    {
        List<string> GetTableNames();
        void RefreshTables();

        void CreateTable(string tableName, List<Database.ColumnDefinition> columns, int primaryKeyIndex);
        void DeleteTable(string tableName);
        void LoadTables(List<Table> tables);
        
        List<Database.TableColumn> GetTableColumns(string tableName);
        List<string> GetColumnNames(string tableName);
        string GetPrimaryKeyColumn(string tableName);
        string GetColumnType(string tableName, string columnName);
        bool CheckPrimaryKeyExists(string tableName, string primaryKeyColumn, string keyValue);

        void InsertRow(string tableName, Dictionary<string, object> rowData);
        void UpdateCellValue(string tableName, Dictionary<string, object> rowData, string columnName, string newValue);
        void DeleteRow(string tableName, Dictionary<string, object> rowData);

        void AddColumn(string tableName, string columnName, string columnType);
        void ModifyColumn(string tableName, int columnIndex, string newName, string newType, bool isPrimaryKey);
        void DeleteColumn(string tableName, string columnName);

        void ChangeColumnType(string tableName, string oldColumnName, string newColumnName, string newColumnType);
        void MakePrimaryKey(string tableName, string newPrimaryKey);
        void ClearTable(string tableName);
    }
}