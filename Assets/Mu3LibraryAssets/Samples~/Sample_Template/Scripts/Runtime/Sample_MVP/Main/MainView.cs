using System;
using System.Collections.Generic;
using Mu3Library.UI.MVP;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.MVP
{
    public class MainView : View
    {
        [SerializeField] private Button _backButton;

        [SerializeField] private MainKeyDescription _keyDescriptionResource;
        private readonly List<MainKeyDescription> _descriptions = new();

        public event Action OnBackButtonClick;



        protected override void LoadFunc()
        {
            base.LoadFunc();

            _backButton.onClick.AddListener(OnBackButtonClickEvent);

            _keyDescriptionResource.gameObject.SetActive(false);
        }

        protected override void UnloadFunc()
        {
            base.UnloadFunc();

            _backButton.onClick.RemoveListener(OnBackButtonClickEvent);
        }

        #region Utility
        public void AddKeyDescription(KeyCode keyCode, string description)
        {
            MainKeyDescription item = Instantiate(_keyDescriptionResource, _keyDescriptionResource.transform.parent);
            item.gameObject.SetActive(true);

            item.SetDescription(keyCode.ToString(), description);

            _descriptions.Add(item);
        }

        public void ClearAllKeyDescriptions()
        {
            foreach (MainKeyDescription item in _descriptions)
            {
                if (item == null || item.gameObject == null)
                {
                    continue;
                }

                Destroy(item.gameObject);
            }

            _descriptions.Clear();
        }
        #endregion

        private void OnBackButtonClickEvent()
        {
            OnBackButtonClick?.Invoke();
        }
    }
}