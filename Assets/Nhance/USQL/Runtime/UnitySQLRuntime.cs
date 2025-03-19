using System.Collections.Generic;
using UnityEngine;

public class UnitySQLRuntime : MonoBehaviour
{
    private const string DATABASE_FILE_PATH = "S:\\GitHub\\UnitySQL\\Assets\\Nhance\\USQL\\Connections\\data.sqlite";
    
    [Header("References")]
    
    [SerializeField] private GridDrawlerUI gridDrawlerUI;
    
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
            connectionItem.Initialize(connection, gridDrawlerUI);
        });
    }

    [ContextMenu("Test Select")]
    public void TestSelect()
    {
        SQLQueryHandler.ExecuteSearchQuery(connections[0].Databases[0], "testTable", new(), out var result);
        gridDrawlerUI.Draw(connections[0].Databases[0].GetPrimaryKeyColumn("testTable"), result);
    }
}
