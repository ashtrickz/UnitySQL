using UnityEngine;
using UnityEngine.UI;

public class ConnectionItemUI : MonoBehaviour
{
    [SerializeField] private Text connectionNameText;
    
    [SerializeField] private Transform databaseLayoutGroup;
    
    [SerializeField] private Transform databaseItemTemplate;

    public void Initialize(DatabaseConnection databaseConnection, GridDrawlerUI gridDrawlerUI)
    {
        connectionNameText.text = databaseConnection.Name;
        
        databaseConnection.Databases.ForEach(database =>
        {
            var databaseItemTransform = Instantiate(databaseItemTemplate, databaseLayoutGroup);
            databaseItemTransform.TryGetComponent<DatabaseItemUI>(out var databaseItem);
            databaseItem.Initialize(database, gridDrawlerUI);
        });
        
        databaseLayoutGroup.gameObject.SetActive(false);
    }

    public void ToggleSubmenu()
    {
        var state = databaseLayoutGroup.gameObject.activeSelf;
        databaseLayoutGroup.gameObject.SetActive(!state);
    }
}
