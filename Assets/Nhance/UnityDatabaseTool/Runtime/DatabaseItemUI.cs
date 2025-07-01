using System.Collections.Generic;
using Nhance.UnityDatabaseTool.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Nhance.UnityDatabaseTool.Runtime
{
    public class DatabaseItemUI : MonoBehaviour
    {
        [SerializeField] private Text databaseNameText;
    
        [SerializeField] private Transform tablesLayoutGroup;
    
        [SerializeField] private Transform tableItemTemplate;
    
        public List<TableItemUI> TableItemUIs => tableItemsUIs;
        private List<TableItemUI> tableItemsUIs = new ();

        public void Initialize(Database database, GridDrawerUI gridDrawerUI)
        {
            databaseNameText.text = database.Name;
        
            database.Tables.ForEach(table =>
            {
                var tableItemTransform = Instantiate(tableItemTemplate, tablesLayoutGroup);
                tableItemTransform.TryGetComponent<TableItemUI>(out var tableItem);
                tableItem.Initialize(database, table, gridDrawerUI);
                tableItemsUIs.Add(tableItem);
            });
        
            tablesLayoutGroup.gameObject.SetActive(false);
        }
    
        public void ToggleSubmenu()
        {
            var state = tablesLayoutGroup.gameObject.activeSelf;
            tablesLayoutGroup.gameObject.SetActive(!state);
        }
    }
}
