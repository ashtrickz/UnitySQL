using System.Collections.Generic;
using Nhance.UnityDatabaseTool.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Nhance.UnityDatabaseTool.Runtime
{
    public class ConnectionItemUI : MonoBehaviour
    {
        [SerializeField] private Text connectionNameText;
    
        [SerializeField] private Transform databaseLayoutGroup;
    
        [SerializeField] private Transform databaseItemTemplate;

        public List<DatabaseItemUI> DatabaseItemUIs => databaseItemsUIs;
        private List<DatabaseItemUI> databaseItemsUIs = new ();

        public void Initialize(DatabaseConnection databaseConnection, GridDrawerUI gridDrawerUI)
        {
            connectionNameText.text = databaseConnection.Name;
        
            databaseConnection.Databases.ForEach(database =>
            {
                var databaseItemTransform = Instantiate(databaseItemTemplate, databaseLayoutGroup);
                databaseItemTransform.TryGetComponent<DatabaseItemUI>(out var databaseItem);
                databaseItem.Initialize(database, gridDrawerUI);
                databaseItemsUIs.Add(databaseItem);
            });
        
            databaseLayoutGroup.gameObject.SetActive(false);
        }

        public void ToggleSubmenu()
        {
            var state = databaseLayoutGroup.gameObject.activeSelf;
            databaseLayoutGroup.gameObject.SetActive(!state);
        }
    }
}
