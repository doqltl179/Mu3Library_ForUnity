using Mu3Library.DI;
using Mu3Library.UI.MVP;
using UnityEngine;
using System.Collections.Generic;
using Mu3Library.Sample.Template.Common;
using Mu3Library.Resource;
using Mu3Library.Attribute;
using Mu3Library.Scene;

namespace Mu3Library.Sample.Template.MVP
{
    public class SampleMVPCore : CoreBase
    {
        private IResourceLoader _resourceLoader;
        private IMVPManager _mvpManager;

        [Space(20)]
        [SerializeField] private bool _useOutPanel = new();

        [ConditionalHide(nameof(_useOutPanel), true)]
        [SerializeField] private bool _interactOutPanelAsClose = new();
        [ConditionalHide(nameof(_useOutPanel), true)]
        [SerializeField] private Color _outPanelColor = new Color(0, 0, 0, 210 / 255f);

        [Space(20)]
        [SerializeField] private KeyCode _notificationOneButtonKey = KeyCode.Q;
        [SerializeField] private KeyCode _notificationTwoButtonKey = KeyCode.W;
        [SerializeField] private KeyCode _oneCurveScaleAnimationPopupKey = KeyCode.E;
        [SerializeField] private KeyCode _twoCurveScaleAnimationPopupKey = KeyCode.R;
        [SerializeField] private KeyCode _bottomToMiddleAnimationPopupKey = KeyCode.A;

        [Space(20)]
        [SerializeField] private KeyCode _loadingScreenOpenKey = KeyCode.G;
        [SerializeField] private KeyCode _loadingScreenCloseKey = KeyCode.H;

        [Space(20)]
        [SerializeField] private KeyCode _closeAllWithoutDefaultKey = KeyCode.Z;
        [SerializeField] private KeyCode _closeAllWithoutDefaultForceKey = KeyCode.X;

        private IPresenter _loadingScreenPresenter = null;



        protected override void Start()
        {
            base.Start();

            _resourceLoader = GetFromCore<CommonCore, IResourceLoader>();
            _mvpManager = GetFromCore<CommonCore, IMVPManager>();

            RegisterViewResources();
            OpenDefaultWindow();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _mvpManager.CloseAll();
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

            else if (Input.GetKeyDown(_loadingScreenOpenKey))
            {
                OpenLoadingScreen();
            }
            else if (Input.GetKeyDown(_loadingScreenCloseKey))
            {
                CloseLoadingScreen();
            }

            else if (Input.GetKeyDown(_closeAllWithoutDefaultKey))
            {
                _mvpManager.CloseAllWithoutDefault();
            }
            else if (Input.GetKeyDown(_closeAllWithoutDefaultForceKey))
            {
                _mvpManager.CloseAllWithoutDefault(true);
            }
        }

        private void OpenDefaultWindow()
        {
            MainArguments args = new MainArguments()
            {
                SceneLoader = GetFromCore<CommonCore, ISceneLoader>(),
                KeyDescriptions = new Dictionary<KeyCode, string>()
                {
                    { _notificationOneButtonKey, "Open one button notification" },
                    { _notificationTwoButtonKey, "Open two button notification" },
                    { _oneCurveScaleAnimationPopupKey, "Open one curve scale animation popup" },
                    { _twoCurveScaleAnimationPopupKey, "Open two curve scale animation popup" },
                    { _bottomToMiddleAnimationPopupKey, "Open bottom to middle animation popup" },

                    { _loadingScreenOpenKey, "Open loading screen" },
                    { _loadingScreenCloseKey, "Close loading screen" },

                    { _closeAllWithoutDefaultKey, "Close all without default" },
                    { _closeAllWithoutDefaultForceKey, "Close all without default (force)" },
                },
            };
            _mvpManager.Open<MainPresenter>(args);
        }

        private void RegisterViewResources()
        {
            const string viewResourcesPath = "Sample_MVP/UIView";
            var resources = _resourceLoader.LoadAll<View>(viewResourcesPath);
            _mvpManager.RegisterViewResources(resources);
        }

        private void OpenNotificationOneButton()
        {
            NotificationArguments args = new()
            {
                OnConfirm = () =>
                {
                    Debug.Log("OnConfirm clicked.");
                    _mvpManager.CloseFocused();
                },
            };

            OutPanelSettings outPanelSettings = GetOutPanelSettings();
            _mvpManager.Open<NotificationPresenter>(args, outPanelSettings);
        }

        private void OpenNotificationTwoButton()
        {
            NotificationArguments args = new()
            {
                OnConfirm = () =>
                {
                    Debug.Log("OnConfirm clicked.");
                    _mvpManager.CloseFocused();
                },
                OnCancel = () =>
                {
                    Debug.Log("OnCancel clicked.");
                    _mvpManager.CloseFocused();
                },
            };

            OutPanelSettings outPanelSettings = GetOutPanelSettings();
            _mvpManager.Open<NotificationPresenter>(args, outPanelSettings);
        }

        private void OpenOneCurveScaleAnimationPopup()
        {
            OutPanelSettings outPanelSettings = GetOutPanelSettings();
            _mvpManager.Open<OneCurveScaleAnimationPopupPresenter>(outPanelSettings);
        }

        private void OpenTwoCurveScaleAnimationPopup()
        {
            OutPanelSettings outPanelSettings = GetOutPanelSettings();
            _mvpManager.Open<TwoCurveScaleAnimationPopupPresenter>(outPanelSettings);
        }

        private void OpenBottomToMiddleAnimationPopup()
        {
            OutPanelSettings outPanelSettings = GetOutPanelSettings();
            _mvpManager.Open<BottomToMiddleAnimationPopupPresenter>(outPanelSettings);
        }

        private void OpenLoadingScreen()
        {
            if(_loadingScreenPresenter == null ||
                _loadingScreenPresenter.ViewState == ViewState.Unloaded)
            {
                _loadingScreenPresenter = _mvpManager.Open<LoadingScreenPresenter>();
            }
        }

        private void CloseLoadingScreen()
        {
            if (_mvpManager.Close(_loadingScreenPresenter))
            {
                _loadingScreenPresenter = null;
            }
        }

        private OutPanelSettings GetOutPanelSettings()
        {
            if (_useOutPanel)
            {
                return new OutPanelSettings()
                {
                    UseOutPanel = true,
                    Color = _outPanelColor,
                    InteractAsClose = _interactOutPanelAsClose,
                };
            }
            else
            {
                return OutPanelSettings.Disabled;
            }
        }
    }
}
