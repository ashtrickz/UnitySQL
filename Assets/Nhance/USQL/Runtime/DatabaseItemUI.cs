using UnityEngine;
using UnityEngine.UI;

public class DatabaseItemUI : MonoBehaviour
{
    [SerializeField] private Text databaseNameText;
    
    [SerializeField] private Transform tablesLayoutGroup;
    
    [SerializeField] private Transform tableItemTemplate;

    public void Initialize(Database database, GridDrawerUI gridDrawerUI)
    {
        databaseNameText.text = database.Name;
        
        database.Tables.ForEach(table =>
        {
            var tableItemTransform = Instantiate(tableItemTemplate, tablesLayoutGroup);
            tableItemTransform.TryGetComponent<TableItemUI>(out var tableItem);
            tableItem.Initialize(database, table, gridDrawerUI);
        });
        
        tablesLayoutGroup.gameObject.SetActive(false);
    }
    
    public void ToggleSubmenu()
    {
        var state = tablesLayoutGroup.gameObject.activeSelf;
        tablesLayoutGroup.gameObject.SetActive(!state);
    }
}
