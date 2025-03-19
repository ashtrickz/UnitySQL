using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class UnitySQLRuntime : MonoBehaviour
{
    private const string DATABASE_FILE_PATH = "Assets/Nhance/USQL/Connections/data.sqlite";
    
    [Header("References")]
    
    [SerializeField] private GridDrawerUI gridDrawerUI;
    
    [SerializeField] private Transform connectionsLayoutGroup;
    
    [SerializeField] private Transform connectionItemTemplate;
    
    private List<DatabaseConnection> connections = new ();

    [ContextMenu("Test Connection")]
    public void TestConnection()
    {
        connections.Add(new DatabaseConnection(DATABASE_FILE_PATH));
        
        connections.ForEach(connection =>
        {
            var connectionItemTransform = Instantiate(connectionItemTemplate, connectionsLayoutGroup);
            connectionItemTransform.TryGetComponent<ConnectionItemUI>(out var connectionItem);
            connectionItem.Initialize(connection, gridDrawerUI);
        });
    }

    [ContextMenu("Test Select")]
    public void TestSelect()
    {
        SQLQueryHandler.ExecuteSearchQuery(connections[0].Databases[0], "testTable", new(), out var result);
        gridDrawerUI.Draw(connections[0].Databases[0].GetPrimaryKeyColumn("testTable"), result);
    }
}
