using System;
using System.Collections.Generic;
using Nhance.UnityDatabaseTool.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Nhance.UnityDatabaseTool.Runtime
{
    public class AddRowModalUI : MonoBehaviour
    {
        [SerializeField] private Transform addRowItemsContainer;
    
        [SerializeField] private Button addButton;
    
        [SerializeField] private Button cancelButton;
    
        [SerializeField] private Transform addRowItemTemplate;
    
        private List<Database.TableColumn> tableColumns;
        private List<AddRowItemUI> addRowItems = new ();

        public Action OnAddRow;
        public Action OnCancel;
    
        public void ShowModal(Database database, string tableName)
        {
            tableColumns = database.GetTableColumns(tableName);
        
            tableColumns.ForEach(column =>
            {
                var itemTransform = Instantiate(addRowItemTemplate, addRowItemsContainer);
                itemTransform.TryGetComponent<AddRowItemUI>(out var addRowItemUI);
                addRowItemUI.Initialize(column.Name);
                addRowItems.Add(addRowItemUI);
            });
        
            addButton.onClick.AddListener(() =>
            {
                Dictionary<string, object> rowData = new ();
            
                Debug.Log($"tableColumns.Count: {tableColumns.Count}; addRowItems.Count : {addRowItems.Count}");

                for (int i = 0; i < addRowItems.Count; i++)
                {
                    rowData.Add(tableColumns[i].Name, addRowItems[i].FieldText);
                }

                database.InsertRow(tableName, rowData);
            
                OnAddRow?.Invoke();
                addButton.onClick.RemoveAllListeners();
                cancelButton.onClick.RemoveAllListeners();
                ClearModal();
                gameObject.SetActive(false);
            });
        
            cancelButton.onClick.AddListener(() =>
            {
                OnCancel?.Invoke();
                addButton.onClick.RemoveAllListeners();
                cancelButton.onClick.RemoveAllListeners();
                ClearModal();
                gameObject.SetActive(false);
            });
        }

        private void ClearModal()
        {
            foreach (Transform child in addRowItemsContainer)
            {
                Destroy(child.gameObject);
            }
            addRowItems = new ();
            tableColumns = new();
        }
    }
}
