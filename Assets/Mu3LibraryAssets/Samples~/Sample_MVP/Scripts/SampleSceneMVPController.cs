using Mu3Library.UI.MVP;
using UnityEngine;
using System.Collections.Generic;

namespace Mu3Library.Sample.MVP
{
    public class SampleSceneMVPController : MonoBehaviour
    {
        private OutPanelParams _defaultOutPanelParams = new OutPanelParams();

        [SerializeField] private KeyCode _notificationOneButtonKey = KeyCode.Q;
        [SerializeField] private KeyCode _notificationTwoButtonKey = KeyCode.W;



        private void Awake()
        {
            MVPFactory.Instance.FillViewResources("UIView");
        }

        private void Start()
        {
            MainArguments args = new MainArguments()
            {
                KeyDescriptions = new Dictionary<KeyCode, string>()
                {
                    { _notificationOneButtonKey, "Open one button notification" },
                    { _notificationTwoButtonKey, "Open two button notification" },
                },
            };
            MVPManager.Instance.Open<MainPresenter>(args);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_notificationOneButtonKey))
            {
                OpenNotificationOneButton();
            }
            else if(Input.GetKeyDown(_notificationTwoButtonKey))
            {
                OpenNotificationTwoButton();
            }
        }

        private void OpenNotificationOneButton()
        {
            NotificationArguments args = new NotificationArguments()
            {
                OnConfirm = () =>
                {
                    Debug.Log("OnConfirm clicked.");
                    MVPManager.Instance.CloseFocused();
                },
            };
            MVPManager.Instance.Open<NotificationPresenter>(args, _defaultOutPanelParams);
        }

        private void OpenNotificationTwoButton()
        {
            NotificationArguments args = new NotificationArguments()
            {
                OnConfirm = () =>
                {
                    Debug.Log("OnConfirm clicked.");
                    MVPManager.Instance.CloseFocused();
                },
                OnCancel = () =>
                {
                    Debug.Log("OnCancel clicked.");
                    MVPManager.Instance.CloseFocused();
                },
            };
            MVPManager.Instance.Open<NotificationPresenter>(args, _defaultOutPanelParams);
        }
    }
}