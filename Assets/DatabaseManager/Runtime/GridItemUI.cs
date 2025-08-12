using UnityEngine;
using UnityEngine.UI;

namespace DatabaseManager.Runtime
{
    public class GridItemUI : MonoBehaviour
    {
        [SerializeField] private Text text;

        public void Initialize(string stringData)
        {
            if (string.IsNullOrEmpty(stringData)) return;
            var splitedString = stringData.Split('/');
            text.text = splitedString.Length > 1 ? splitedString[splitedString.Length - 1] : stringData;
        }
    
        public void InitializePK(string stringData)
        {
            text.text = $"PK {stringData}";
        }
    }
}
