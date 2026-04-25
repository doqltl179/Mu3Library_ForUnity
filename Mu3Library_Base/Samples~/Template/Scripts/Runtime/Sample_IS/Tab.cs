using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.IS
{
    public abstract class Tab : MonoBehaviour
    {
        [SerializeField] private Toggle _toggle;
        [SerializeField] private TextMeshProUGUI _text;

        public bool IsOn => _toggle.isOn;

        public event Action<Tab> OnValueChanged;



        private void Awake()
        {
            _toggle.onValueChanged.AddListener(OnValueChangedEvent);
        }

        private void OnDestroy()
        {
            _toggle.onValueChanged.RemoveListener(OnValueChangedEvent);
        }

        #region Utility
        public void SetIsOn(bool isOn, bool force = false)
        {
            if (force && _toggle.isOn == isOn)
            {
                _toggle.SetIsOnWithoutNotify(!isOn);
            }

            _toggle.isOn = isOn;
        }

        public void SetTabName(string name)
        {
            _text.text = name;
        }

        public void SetToggleGroup(ToggleGroup toggleGroup)
        {
            _toggle.group = toggleGroup;
        }
        #endregion

        private void OnValueChangedEvent(bool isOn)
        {
            _toggle.image.color = isOn ? Color.white : Color.gray;

            OnValueChanged?.Invoke(this);
        }
    }
}
