using System.Collections.Generic;
using UnityEngine;

public class GridDrawerUI : MonoBehaviour
{
    [SerializeField] private Transform gridTransform;
    
    [SerializeField] private Transform columnTemplate;
    
    [SerializeField] private Transform gridItemTemplate;

    public void Draw(string primaryKey, (string[], List<string[]>) data)
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
                {
                    if (primaryKey == data.Item1[i])
                        gridItemUI.InitializePK(data.Item1[i]);
                    else
                        gridItemUI.Initialize(data.Item1[i]);
                }
                else
                {
                    gridItemUI.Initialize(data.Item2[j][i]);
                }
            }
        }
    }
}
