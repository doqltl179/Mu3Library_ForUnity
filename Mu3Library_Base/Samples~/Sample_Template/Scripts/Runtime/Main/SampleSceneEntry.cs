using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Main
{
    public class SampleSceneEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _button;

        [Space(20)]
        [SerializeField] private GameObject _highlightObject;

        private string _sceneName;
        public string SceneName => _sceneName;

        public event Action<SampleSceneEntry> OnEntryClicked;
        public event Action<SampleSceneEntry> OnPointerEntered;
        public event Action<SampleSceneEntry> OnPointerExited;



        private void Awake()
        {
            _button.onClick.AddListener(OnClickEntryButton);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            OnPointerEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData _)
        {
            OnPointerExited?.Invoke(this);
        }

        #region Utility
        public void ApplyOnPointerExited(Action<SampleSceneEntry> action)
        {
            OnPointerExited = action;
        }

        public void ApplyOnPointerEntered(Action<SampleSceneEntry> action)
        {
            OnPointerEntered = action;
        }

        public void ApplyOnClickListener(Action<SampleSceneEntry> action)
        {
            OnEntryClicked = action;
        }

        public void SetActiveHighlight(bool active)
        {
            _highlightObject.SetActive(active);
        }

        public void SetSceneName(string sceneName)
        {
            _sceneName = sceneName;
        }

        public void SetTitle(string title)
        {
            _titleText.text = title;
        }
        #endregion

        private void OnClickEntryButton()
        {
            OnEntryClicked?.Invoke(this);
        }
    }
}
