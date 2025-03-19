using UnityEngine;
using UnityEngine.UI;

public class TableItemUI : MonoBehaviour
{
    [SerializeField] private Text tableNameText;
    [SerializeField] private Button button;

    private Database database;
    private Table table;
    private GridDrawlerUI gridDrawlerUI;
    public void Initialize(Database database, Table table, GridDrawlerUI gridDrawlerUI)
    {
        tableNameText.text = table.Name;
        this.gridDrawlerUI = gridDrawlerUI;
        this.database = database;
        this.table = table;
        
        button.onClick.AddListener(DrawTableOverview);
    }

    public void DrawTableOverview()
    {
        SQLQueryHandler.ExecuteSearchQuery(database, table.Name, new(), out var result);
        gridDrawlerUI.Draw(database.GetPrimaryKeyColumn(table.Name), result);
    }
}
