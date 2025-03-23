using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
