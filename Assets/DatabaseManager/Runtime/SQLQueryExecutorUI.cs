using System;
using DatabaseManager.Data;
using DatabaseManager.General;
using UnityEngine;
using UnityEngine.UI;

namespace DatabaseManager.Runtime
{
    public class SQLQueryExecutorUI : MonoBehaviour
    {
        [SerializeField] private GridDrawerUI gridDrawer;
        [SerializeField] private InputField inputField;
    
        private Database database;

        public Action<Database> OnDatabaseChange;

        private void Awake()
        {
            OnDatabaseChange = database =>
            {
                this.database = database;
            };
        }

        public void ExecuteSQLQuery()
        {
            var sqlQuery = inputField.text;
            database.SQLQuery = sqlQuery;
            var result = SQLQueryHandler.ExecuteSQLQuery(database);
            if (result != default)
                gridDrawer.Draw(result);
        }
    }
}
