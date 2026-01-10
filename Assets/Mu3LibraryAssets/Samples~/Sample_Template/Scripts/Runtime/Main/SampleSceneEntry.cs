using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Main
{
    public class SampleSceneEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _button;



        #region Utility
        public void ApplyOnClickListener(UnityEngine.Events.UnityAction action)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(action);
        }

        public void SetTitle(string title)
        {
            _titleText.text = title;
        }
        #endregion
    }
}
