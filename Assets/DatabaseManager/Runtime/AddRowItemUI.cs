using UnityEngine;
using UnityEngine.UI;

namespace DatabaseManager.Runtime
{
    public class AddRowItemUI : MonoBehaviour
    {
        [SerializeField] private Text fieldNaming;
    
        [SerializeField] private InputField inputField;

        public void Initialize(string fieldName)
        {
            fieldNaming.text = fieldName;
        }
    
        public string FieldText => inputField.text;
    }
}
