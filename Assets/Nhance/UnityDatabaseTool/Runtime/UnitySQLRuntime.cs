using System;
using System.Collections.Generic;
using Nhance.UnityDatabaseTool.Data;
using Nhance.UnityDatabaseTool.General;
using UnityEngine;
using UnityEngine.UI;

namespace Nhance.UnityDatabaseTool.Runtime
{
    public class UnitySqlRuntime : MonoBehaviour
    {
        [Header("Settings")]
    
        [SerializeField] private string databaseFilePath = "Assets/Nhance/USQL/Connections/data.sqlite";

        [Header("Tabs")] 
    
        [SerializeField] private List<ButtonPanelPair> tabs = new();

        [Header("References")] 
    
        [SerializeField] private Transform tabsContainer;
    
        [SerializeField] private GridDrawerUI overviewGridDrawerUI;
    
        [SerializeField] private SQLQueryExecutorUI sqlQueryExecutorUI;
    
        [SerializeField] private SubmitModalUI submitModalUI;
    
        [SerializeField] private AddRowModalUI addRowModalUI;
    
        [SerializeField] private Transform connectionsLayoutGroup;
    
        [SerializeField] private Transform connectionItemTemplate;
    
        private List<DatabaseConnection> connections = new ();
    
        public Database Database => database;
        private Database database;
    
        public Table Table => table;
        private Table table;
    
        public Action<Database> OnDatabaseChanged;
    
        private void Start()
        {
            Connection();
        
            OnDatabaseChanged = new(database =>
            {
                this.database = database;
                CloseAllTabs();
                tabs[0].panel.gameObject.SetActive(true);
                sqlQueryExecutorUI.OnDatabaseChange.Invoke(database);
            });
        
            overviewGridDrawerUI.Initialize(submitModalUI);
            overviewGridDrawerUI.OnAddRowButtonClicked += () =>
            {
                addRowModalUI.gameObject.SetActive(true);
                addRowModalUI.ShowModal(database, table.Name);

                addRowModalUI.OnAddRow += () =>
                {
                    SQLQueryHandler.ExecuteSearchQuery(database, table.Name, new(), out var result);
                    overviewGridDrawerUI.Draw(database, table, result);
                };
            };
        
            submitModalUI.OnSubmit += () =>
            {
                SQLQueryHandler.ExecuteSearchQuery(database, table.Name, new(), out var result);
                overviewGridDrawerUI.Draw(database, table, result);
            };
            submitModalUI.gameObject.SetActive(false);
        
            tabs.ForEach(item =>
            {
                item.button.onClick.AddListener(() =>
                {
                    CloseAllTabs();
                    item.panel.gameObject.SetActive(true);
                });
            });
        
            CloseAllTabs();
            tabsContainer.gameObject.SetActive(false);
            addRowModalUI.gameObject.SetActive(false);
        }
    
        public void Connection()
        {
            connections.Add(new DatabaseConnection(databaseFilePath, DatabaseConnection.EConnectionType.SQLite));
        
            connections.ForEach(connection =>
            {
                var connectionItemTransform = Instantiate(connectionItemTemplate, connectionsLayoutGroup);
                connectionItemTransform.TryGetComponent<ConnectionItemUI>(out var connectionItem);
                connectionItem.Initialize(connection, overviewGridDrawerUI);
            
                connectionItem.DatabaseItemUIs.ForEach(databaseItem =>
                {
                    databaseItem.TableItemUIs.ForEach(tableItem =>
                    {
                        tableItem.OnTableChose += (database, table) =>
                        {
                            this.table = table;
                            tabsContainer.gameObject.SetActive(true);
                            OnDatabaseChanged?.Invoke(database);
                        };
                    });
                });
            });
        }

        public void CloseAllTabs()
        {
            tabs.ForEach(item => item.panel.gameObject.SetActive(false));
        }

        [Serializable]
        public struct ButtonPanelPair
        {
            public Button button;
            public Transform panel;
        }
    }
}
