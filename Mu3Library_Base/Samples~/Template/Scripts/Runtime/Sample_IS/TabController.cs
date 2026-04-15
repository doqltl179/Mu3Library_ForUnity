using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.IS
{
    public abstract class TabController : MonoBehaviour
    {
        [SerializeField] private ToggleGroup _toggleGroup;
        [SerializeField] private Tab _tabResource;

        private readonly List<Tab> _tabs = new();

        private Tab _currentTab = null;
        public Tab CurrentTab => _currentTab;

        public event Action<Tab> OnTabChanged;



        private void Awake()
        {
            _tabResource.gameObject.SetActive(false);
        }

        #region Utility
        public Tab AddTab()
        {
            var tab = Instantiate(_tabResource, _tabResource.transform.parent);
            tab.gameObject.SetActive(true);

            tab.SetIsOn(false, true);
            tab.SetToggleGroup(_toggleGroup);

            tab.OnValueChanged += OnTabChangedEvent;

            _tabs.Add(tab);

            return tab;
        }

        public void ChangeTabToFirst(bool force = false)
            => ChangeTab(0, force);

        public void ChangeTab(int index, bool force = false)
        {
            if (index < 0 || index >= _tabs.Count)
            {
                Debug.LogError($"Invalid tab index: {index}");
                return;
            }

            _tabs[index].SetIsOn(true, force);
        }
        #endregion

        private void OnTabChangedEvent(Tab changedTab)
        {
            if (changedTab.IsOn)
            {
                _currentTab = changedTab;
            }

            OnTabChanged?.Invoke(changedTab);
        }
    }
}
