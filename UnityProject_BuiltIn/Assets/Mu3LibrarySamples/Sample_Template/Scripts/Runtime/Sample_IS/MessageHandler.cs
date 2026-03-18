using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.IS
{
    public class MessageHandler : MonoBehaviour
    {
        [SerializeField] private GameObject _messageObj;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _cancelButton;

        private Action _onCancel;



        private void Awake()
        {
            _cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }

        private void OnDestroy()
        {
            _cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
        }

        private void Start()
        {
            _messageObj.SetActive(false);
        }

        #region Utility
        public void ShowMessage(string message, Action onCancel = null)
        {
            _messageObj.SetActive(true);
            _messageText.text = message;

            _onCancel = onCancel;
        }

        public void HideMessage()
        {
            _messageObj.SetActive(false);
            _onCancel = null;
        }
        #endregion

        private void OnCancelButtonClicked()
        {
            _messageObj.SetActive(false);

            _onCancel?.Invoke();
        }
    }
}
