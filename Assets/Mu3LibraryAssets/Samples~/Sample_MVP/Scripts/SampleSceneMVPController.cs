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
        [SerializeField] private KeyCode _oneCurveScaleAnimationPopupKey = KeyCode.E;
        [SerializeField] private KeyCode _twoCurveScaleAnimationPopupKey = KeyCode.R;
        [SerializeField] private KeyCode _bottomToMiddleAnimationPopupKey = KeyCode.A;



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
                    { _oneCurveScaleAnimationPopupKey, "Open one curve scale animation popup" },
                    { _twoCurveScaleAnimationPopupKey, "Open two curve scale animation popup" },
                    { _bottomToMiddleAnimationPopupKey, "Open bottom to middle animation popup" },
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
            else if (Input.GetKeyDown(_notificationTwoButtonKey))
            {
                OpenNotificationTwoButton();
            }
            else if (Input.GetKeyDown(_oneCurveScaleAnimationPopupKey))
            {
                OpenOneCurveScaleAnimationPopup();
            }
            else if (Input.GetKeyDown(_twoCurveScaleAnimationPopupKey))
            {
                OpenTwoCurveScaleAnimationPopup();
            }
            else if (Input.GetKeyDown(_bottomToMiddleAnimationPopupKey))
            {
                OpenBottomToMiddleAnimationPopup();
            }
        }

        private void OpenNotificationOneButton()
        {
            NotificationArguments args = new()
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
            NotificationArguments args = new()
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

        private void OpenOneCurveScaleAnimationPopup()
        {
            OneCurveScaleAnimationPopupArguments args = new()
            {

            };
            MVPManager.Instance.Open<OneCurveScaleAnimationPopupPresenter>(args, _defaultOutPanelParams);
        }

        private void OpenTwoCurveScaleAnimationPopup()
        {
            TwoCurveScaleAnimationPopupArguments args = new()
            {

            };
            MVPManager.Instance.Open<TwoCurveScaleAnimationPopupPresenter>(args, _defaultOutPanelParams);
        }

        private void OpenBottomToMiddleAnimationPopup()
        {
            BottomToMiddleAnimationPopupArguments args = new()
            {

            };
            MVPManager.Instance.Open<BottomToMiddleAnimationPopupPresenter>(args, _defaultOutPanelParams);
        }
    }
}