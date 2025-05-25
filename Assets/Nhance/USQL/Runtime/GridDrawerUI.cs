using System;
using System.Collections.Generic;
using Nhance.USQL.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class GridDrawerUI : MonoBehaviour
{
    [SerializeField] private Transform gridTransform;
    
    [SerializeField] private Transform columnTemplate;
    
    [SerializeField] private Transform gridItemTemplate;
    
    [SerializeField] private Transform gridAddRowButtonTemplate;
    
    [SerializeField] private Transform gridDeleteRowButtonTemplate;
    
    private SubmitModalUI submitModalUI;
    
    public Action OnAddRowButtonClicked;

    public void Initialize(SubmitModalUI submitModalUI)
    {
        this.submitModalUI = submitModalUI;
    }

    public void Draw((string[], List<string[]>) data)
    {
        foreach (Transform child in gridTransform)
        {
            Destroy(child.gameObject);
        }
        
        var columnCount = data.Item1.Length;
        var rowCount = data.Item2.Count;

        for (int i = 0; i < columnCount; i++)
        {
            var column = Instantiate(columnTemplate, gridTransform);

            for (int j = -1; j < rowCount; j++)
            {
                var item = Instantiate(gridItemTemplate, column);
                item.TryGetComponent<GridItemUI>(out var gridItemUI);
                if (j == -1)
                        gridItemUI.Initialize(data.Item1[i]);
                else
                    gridItemUI.Initialize(data.Item2[j][i]);
            }
        }
    }
    public void Draw(Database database, Table table, (string[], List<string[]>) data)
    {
        foreach (Transform child in gridTransform)
        {
            Destroy(child.gameObject);
        }
        
        var columnCount = data.Item1.Length;
        var rowCount = data.Item2.Count;

        var primaryKey = database.GetPrimaryKeyColumn(table.Name);
        
        int primaryKeyIndex = -1;
        
        for (int i = 0; i < columnCount; i++)
        {
            if (primaryKey == data.Item1[i])
                primaryKeyIndex = i;
        }
        
        for (int i = 0; i < columnCount; i++)
        {
            var column = Instantiate(columnTemplate, gridTransform);

            for (int j = -1; j < rowCount; j++)
            {
                var item = Instantiate(gridItemTemplate, column);
                
                item.TryGetComponent<GridItemUI>(out var gridItemUI);
                
                if (j == -1)
                {
                    if (primaryKeyIndex == i)
                        gridItemUI.InitializePK(data.Item1[i]);
                    else
                        gridItemUI.Initialize(data.Item1[i]);
                }
                else
                {
                    gridItemUI.Initialize(data.Item2[j][i]);
                }

                if (j + 1 == rowCount && i == 0)
                {
                    //AddRowButton
                    var addRowButtonTransform = Instantiate(gridAddRowButtonTemplate, column);
                    addRowButtonTransform.TryGetComponent<Button>(out var button);
                    button.onClick.AddListener(() => OnAddRowButtonClicked?.Invoke());
                }
            }
        }
        
        var deleteButtonsColumn = Instantiate(columnTemplate, gridTransform);
        
        var deleteColumnCaption = Instantiate(gridItemTemplate, deleteButtonsColumn);
        deleteColumnCaption.TryGetComponent<GridItemUI>(out var deleteColumnCaptionUI);
        deleteColumnCaptionUI.Initialize(null);

        for (int i = 0; i < rowCount; i++)
        {
            var item = Instantiate(gridDeleteRowButtonTemplate, deleteButtonsColumn);
            item.TryGetComponent<GridItemUI>(out var gridItemUI);
            gridItemUI.Initialize(null);
            item.TryGetComponent<Button>(out var button);

            var index = i;
            button.onClick.AddListener(() =>
            {
                database.SQLQuery = $"DELETE FROM {table.Name} WHERE {data.Item1[primaryKeyIndex]}='{data.Item2[index][primaryKeyIndex]}'";
                submitModalUI.gameObject.SetActive(true);
                submitModalUI.ShowModal(database, $"Delete row with {data.Item1[primaryKeyIndex]} {data.Item2[index][primaryKeyIndex]} from {table.Name}?");
            });
        }
    }
}
