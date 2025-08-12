using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DatabaseManager.SQLite
{
    public interface ISQLiteConnection : IDisposable
    {
        IntPtr Handle { get; }
        string DatabasePath { get; }
        int LibVersionNumber { get; }
        bool TimeExecution { get; set; }
        bool Trace { get; set; }
        Action<string> Tracer { get; set; }
        bool StoreDateTimeAsTicks { get; }
        bool StoreTimeSpanAsTicks { get; }
        string DateTimeStringFormat { get; }
        TimeSpan BusyTimeout { get; set; }
        IEnumerable<TableMapping> TableMappings { get; }
        bool IsInTransaction { get; }

        event EventHandler<NotifyTableChangedEventArgs> TableChanged;

        void Backup(string destinationDatabasePath, string databaseName = "main");
        void BeginTransaction();
        void Close();
        void Commit();
        SQLiteCommand CreateCommand(string cmdText, params object[] ps);
        SQLiteCommand CreateCommand(string cmdText, Dictionary<string, object> args);
        int CreateIndex(string indexName, string tableName, string[] columnNames, bool unique = false);
        int CreateIndex(string indexName, string tableName, string columnName, bool unique = false);
        int CreateIndex(string tableName, string columnName, bool unique = false);
        int CreateIndex(string tableName, string[] columnNames, bool unique = false);

        int CreateIndex<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(Expression<Func<T, object>> property, bool unique = false);

        CreateTableResult CreateTable<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(CreateFlags createFlags = CreateFlags.None);

        CreateTableResult CreateTable(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type ty, CreateFlags createFlags = CreateFlags.None);

        CreateTablesResult CreateTables<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T2>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new();

        CreateTablesResult CreateTables<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T2,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T3>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new();

        CreateTablesResult CreateTables<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T2,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T3,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T4>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
            where T4 : new();

        CreateTablesResult CreateTables<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T2,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T3,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T4,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T5>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
            where T4 : new()
            where T5 : new();
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires 'DynamicallyAccessedMemberTypes.All' on each input 'Type' instance.")]
#endif
        CreateTablesResult CreateTables(CreateFlags createFlags = CreateFlags.None, params Type[] types);

        IEnumerable<T> DeferredQuery<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(string query, params object[] args) where T : new();

        IEnumerable<object> DeferredQuery(TableMapping map, string query, params object[] args);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'objectToDelete'.")]
#endif
        int Delete(object objectToDelete);

        int Delete<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(object primaryKey);

        int Delete(object primaryKey, TableMapping map);

        int DeleteAll<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>();

        int DeleteAll(TableMapping map);

        int DropTable<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>();

        int DropTable(TableMapping map);
        void EnableLoadExtension(bool enabled);
        void EnableWriteAheadLogging();
        int Execute(string query, params object[] args);
        T ExecuteScalar<T>(string query, params object[] args);

        T Find<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(object pk) where T : new();

        object Find(object pk, TableMapping map);

        T Find<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(Expression<Func<T, bool>> predicate) where T : new();

        T FindWithQuery<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(string query, params object[] args) where T : new();

        object FindWithQuery(TableMapping map, string query, params object[] args);

        T Get<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(object pk) where T : new();

        object Get(object pk, TableMapping map);

        T Get<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(Expression<Func<T, bool>> predicate) where T : new();

        TableMapping GetMapping(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type type, CreateFlags createFlags = CreateFlags.None);

        TableMapping GetMapping<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(CreateFlags createFlags = CreateFlags.None);

        List<SQLiteConnection.ColumnInfo> GetTableInfo(string tableName);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
#endif
        int Insert(object obj);
        int Insert(
            object obj,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type objType);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
#endif
        int Insert(object obj, string extra);
        int Insert(
            object obj,
            string extra,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type objType);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
#endif
        int InsertAll(IEnumerable objects, bool runInTransaction = true);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
#endif
        int InsertAll(IEnumerable objects, string extra, bool runInTransaction = true);
        int InsertAll(
            IEnumerable objects,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type objType,
            bool runInTransaction = true);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
#endif
        int InsertOrReplace(object obj);

        int InsertOrReplace(
            object obj,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type objType);

        List<T> Query<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>(string query, params object[] args) where T : new();

        List<object> Query(TableMapping map, string query, params object[] args);
        List<T> QueryScalars<T>(string query, params object[] args);
        void ReKey(string key);
        void ReKey(byte[] key);
        void Release(string savepoint);
        void Rollback();
        void RollbackTo(string savepoint);
        void RunInTransaction(Action action);
        string SaveTransactionPoint();
        TableQuery<T> Table<
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            T>() where T : new();
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
#endif
        int Update(object obj);
        int Update(
            object obj,
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type objType);
#if NET8_0_OR_GREATER
		[RequiresUnreferencedCode ("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
#endif
        int UpdateAll(IEnumerable objects, bool runInTransaction = true);
    }
}