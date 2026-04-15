using System;
using Mu3Library.UI.MVP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.MVP
{
    public class NotificationView : View
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private TextMeshProUGUI _confirmButtonText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _cancelButtonText;
        [SerializeField] private Button _cancelButton;



        protected override void LoadFunc()
        {
            _messageText.text = "";

            _confirmButtonText.text = "";
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.gameObject.SetActive(false);

            _cancelButtonText.text = "";
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.gameObject.SetActive(false);
        }

        #region Utility
        public void SetMessage(string message)
        {
            _messageText.text = message;
        }

        public void SetConfirmButton(string buttonText, Action onClick)
        {
            _confirmButtonText.text = buttonText;
            _confirmButton.onClick.AddListener(() => { onClick?.Invoke(); });
            _confirmButton.gameObject.SetActive(true);
        }

        public void SetCancelButton(string buttonText, Action onClick)
        {
            _cancelButtonText.text = buttonText;
            _cancelButton.onClick.AddListener(() => { onClick?.Invoke(); });
            _cancelButton.gameObject.SetActive(true);
        }
        #endregion
    }
}