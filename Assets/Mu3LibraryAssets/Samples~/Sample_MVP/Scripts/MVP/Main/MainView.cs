using System.Collections.Generic;
using Mu3Library.UI.MVP;
using UnityEngine;

namespace Mu3Library.Sample.MVP
{
    public class MainView : View
    {
        [SerializeField] private MainKeyDescription _keyDescriptionResource;
        private readonly List<MainKeyDescription> _descriptions = new();



        protected override void LoadFunc()
        {
            base.LoadFunc();

            _keyDescriptionResource.gameObject.SetActive(false);
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
    }
}