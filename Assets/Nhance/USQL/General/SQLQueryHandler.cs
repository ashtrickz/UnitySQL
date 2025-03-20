using System;
using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
using UnityEngine;

public static class SQLQueryHandler
{
    public static void ExecuteSearchQuery(Database database, string table, List<SearchFilter> searchFilters, out (string[], List<string[]>) result)
    {
        (string[], List<string[]>) tempResult = new();
        try
        {
            List<string> conditions = new ();

            foreach (var filter in searchFilters)
            {
                if (!string.IsNullOrEmpty(filter.Value))
                {
                    conditions.Add($"{filter.Column} {filter.Operator} '{filter.Value}'");
                }
            }

            string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            string query = $"SELECT * FROM {table} {whereClause};";

            using (var connection = new SqliteConnection($"Data Source={database.Path};Version=3;"))
            {
                connection.Open();
                using (var dbCommand = connection.CreateCommand())
                {
                    dbCommand.CommandText = query;
                    tempResult = ReadTableResults(dbCommand);
                }
            }

            Debug.Log("Search executed successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error: " + ex.Message);
        }
        result = (tempResult.Item1, tempResult.Item2);
    }
    
    public static (string[], List<string[]>) ExecuteSQLQuery(Database database)
    {
        (string[], List<string[]>) tempResult = new();
        try
        {
            using (var connection = new SqliteConnection($"Data Source={database.Path};Version=3;"))
            {
                connection.Open();
                using (var dbCommand = connection.CreateCommand())
                {
                    dbCommand.CommandText = database.SQLQuery;

                    if (database.SQLQuery.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    {
                        tempResult = ReadTableResults(dbCommand);
                    }
                    else
                    {
                        int affectedRows = dbCommand.ExecuteNonQuery();
                        Debug.Log($"Query executed successfully. Affected rows: {affectedRows}");
                    }
                }
            }

            //DrawConnectionsPanel();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error: " + ex.Message);
        }
        return (tempResult.Item1, tempResult.Item2);
    }
    
    private static (string[], List<string[]>) ReadTableResults(IDbCommand dbCommand)
    {
        using (IDataReader reader = dbCommand.ExecuteReader())
        {
            int columnCount = reader.FieldCount;
            var columnNames = new string[columnCount];

            for (int i = 0; i < columnCount; i++)
            {
                columnNames[i] = reader.GetName(i);
            }
            
            var tableData = new List<string[]>();

            while (reader.Read())
            {
                string[] row = new string[columnCount];
                for (int i = 0; i < columnCount; i++)
                {
                    row[i] = reader.GetValue(i).ToString();
                }

                tableData.Add(row);
            }
            
            Debug.Log("Query executed successfully.");
            return (columnNames, tableData);
        }
        
    }
    
    public class SearchFilter
    {
        public string Column;
        public string Operator;
        public string Value;
    }
}
