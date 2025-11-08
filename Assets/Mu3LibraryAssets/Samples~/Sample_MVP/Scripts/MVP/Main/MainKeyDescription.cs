using TMPro;
using UnityEngine;

namespace Mu3Library.Sample.MVP
{
    public class MainKeyDescription : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _keyText;
        [SerializeField] private TextMeshProUGUI _descriptionText;



        #region Utility
        public void SetDescription(string key, string description)
        {
            _keyText.text = key;
            _descriptionText.text = description;
        }
        #endregion
    }
}