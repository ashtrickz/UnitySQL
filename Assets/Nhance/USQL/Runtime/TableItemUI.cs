using System;
using Nhance.USQL.Data;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class TableItemUI : MonoBehaviour
{
    [SerializeField] private Text tableNameText;
    [SerializeField] private Button button;

    private Database database;
    private Table table;
    private GridDrawerUI gridDrawerUI;

    public Action<Database, Table> OnTableChose;

    public void Initialize(Database database, Table table, GridDrawerUI gridDrawerUI)
    {
        tableNameText.text = table.Name;
        this.gridDrawerUI = gridDrawerUI;
        this.database = database;
        this.table = table;
        
        button.onClick.AddListener(DrawTableOverview);
    }

    public void DrawTableOverview()
    {
        SQLQueryHandler.ExecuteSearchQuery(database, table.Name, new(), out var result);
        gridDrawerUI.Draw(database, table, result);
        OnTableChose.Invoke(database, table);
    }
}
