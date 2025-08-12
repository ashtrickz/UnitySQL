using System;
using System.Collections.Generic;

namespace DatabaseManager.SQLite
{
    public class CreateTablesResult
    {
        public Dictionary<Type, CreateTableResult> Results { get; private set; }

        public CreateTablesResult()
        {
            Results = new Dictionary<Type, CreateTableResult>();
        }
    }
}