using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UnitySQLRuntime : MonoBehaviour
{
    private const string DATABASE_FILE_PATH = "Assets/Nhance/USQL/Connections/data.sqlite";

    [Header("Tabs")] 
    
    [SerializeField] private List<ButtonPanelPair> tabs = new();
    
    [Header("References")]
    
    [SerializeField] private GridDrawerUI overviewGridDrawerUI;
    
    [SerializeField] private SQLQueryExecutorUI sqlQueryExecutorUI;
    
    [SerializeField] private Transform connectionsLayoutGroup;
    
    [SerializeField] private Transform connectionItemTemplate;
    
    private List<DatabaseConnection> connections = new ();
    
    public Database Database => database;
    private Database database;
    
    public Action<Database> OnDatabaseChanged;
    
    private void Start()
    {
        OnDatabaseChanged = new(database =>
        {
            this.database = database;
            CloseAllTabs();
            tabs[0].panel.gameObject.SetActive(true);
            sqlQueryExecutorUI.OnDatabaseChange.Invoke(database);
        });
        
        tabs.ForEach(item =>
        {
            item.button.onClick.AddListener(() =>
            {
                CloseAllTabs();
                item.panel.gameObject.SetActive(true);
            });
        });
        
        CloseAllTabs();
    }

    [ContextMenu("Test Connection")]
    public void TestConnection()
    {
        connections.Add(new DatabaseConnection(DATABASE_FILE_PATH));
        
        connections.ForEach(connection =>
        {
            var connectionItemTransform = Instantiate(connectionItemTemplate, connectionsLayoutGroup);
            connectionItemTransform.TryGetComponent<ConnectionItemUI>(out var connectionItem);
            connectionItem.Initialize(connection, overviewGridDrawerUI);
            
            connectionItem.DatabaseItemUIs.ForEach(databaseItem =>
            {
                databaseItem.TableItemUIs.ForEach(tableItem =>
                {
                    tableItem.OnTableChose += database =>
                    {
                        OnDatabaseChanged?.Invoke(database);
                    };
                });
            });
        });
    }

    [ContextMenu("Test Select")]
    public void TestSelect()
    {
        SQLQueryHandler.ExecuteSearchQuery(connections[0].Databases[0], "testTable", new(), out var result);
        overviewGridDrawerUI.Draw(connections[0].Databases[0].GetPrimaryKeyColumn("testTable"), result);
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
