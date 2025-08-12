using System;
using DatabaseManager.Data;
using DatabaseManager.General;
using UnityEngine;
using UnityEngine.UI;

namespace DatabaseManager.Runtime
{
    public class SubmitModalUI : MonoBehaviour
    {
        [SerializeField] private Text text;
    
        [SerializeField] private Button submitButton;
    
        [SerializeField] private Button cancelButton;
    
        public Action OnSubmit;
        public Action OnCancel;

        public void ShowModal(Database database, string modalText)
        {
            text.text = modalText;
        
            submitButton.onClick.AddListener(() =>
            {
                SQLQueryHandler.ExecuteSQLQuery(database);
                OnSubmit?.Invoke();
                submitButton.onClick.RemoveAllListeners();
                cancelButton.onClick.RemoveAllListeners();
                gameObject.SetActive(false);
            });
        
            cancelButton.onClick.AddListener(() =>
            {
                OnCancel?.Invoke();
                submitButton.onClick.RemoveAllListeners();
                cancelButton.onClick.RemoveAllListeners();
                gameObject.SetActive(false);
            });
        }
    }
}
