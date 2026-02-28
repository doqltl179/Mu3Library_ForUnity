using System.Linq;
using Mu3Library.Attribute;
using Mu3Library.DI;
using Mu3Library.Resource;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


#if TEMPLATE_INPUTSYSTEM_SUPPORT
using Mu3Library.IS;
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class SampleISCore : CoreBase
    {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
        [Inject(typeof(ISCore))] private IInputSystemManager _inputSystemManager;
#endif

        [Inject(typeof(SceneCore))] private ISceneLoader _sceneLoader;
        [Inject(typeof(ResourceCore))] private IResourceLoader _resourceLoader;

        [Title("UI Elements")]
        [SerializeField] private InputActionMapTabController _inputActionMapTabController;
        [SerializeField] private DeviceTabController _deviceTabController;

        [Space(20)]
        [SerializeField] private InputActionPage _inputActionPageResource;
        private readonly Dictionary<string, Dictionary<string, InputActionPage>> _inputActionPages = new();

        private DeviceTab _currentDeviceTab;
        private InputActionMapTab _currentInputActionMapTab;

        [Space(20)]
        [SerializeField] private Button _backButton;



        protected override void Awake()
        {
            base.Awake();

            _inputActionPageResource.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            RegisterUiEvents();
        }

        private void OnDisable()
        {
            UnregisterUiEvents();
        }

        protected override void Start()
        {
            base.Start();

#if TEMPLATE_INPUTSYSTEM_SUPPORT
            InputActionAsset inputActionAsset = _resourceLoader.Load<InputActionAsset>("Sample_IS/InputSystemActions");
            _inputSystemManager.AddInputActionAsset(inputActionAsset);
            _inputSystemManager.SetInputActionAssetEnable(true);

            // var allDevices = InputSystem.devices;
            var allDevices = inputActionAsset.controlSchemes
                .SelectMany(scheme => scheme.deviceRequirements)
                .Select(req => req.controlPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => InputSystem.FindControl(path)?.device)
                .Where(device => device != null)
                .Distinct();

            foreach (InputDevice device in allDevices)
            {
                var tab = _deviceTabController.AddTab();
                if (tab is DeviceTab deviceTab)
                {
                    deviceTab.SetDevice(device);
                }
            }

            foreach (InputActionMap inputActionMap in inputActionAsset.actionMaps)
            {
                var tab = _inputActionMapTabController.AddTab();
                if (tab is InputActionMapTab inputActionMapTab)
                {
                    inputActionMapTab.SetInputActionMap(inputActionMap);
                }

                foreach (var device in allDevices)
                {
                    if (!_inputActionPages.ContainsKey(device.name))
                    {
                        _inputActionPages[device.name] = new Dictionary<string, InputActionPage>();
                    }

                    var inputActionPage = Instantiate(_inputActionPageResource, _inputActionPageResource.transform.parent);
                    inputActionPage.gameObject.SetActive(false);
                    inputActionPage.name = $"{device.name}_{inputActionMap.name}";

                    foreach (var action in inputActionMap.actions)
                    {
                        inputActionPage.AddInputAction(device, action);
                    }

                    _inputActionPages[device.name][inputActionMap.name] = inputActionPage;
                }
            }
#endif

            _deviceTabController.OnTabChanged += OnTabChanged;
            _inputActionMapTabController.OnTabChanged += OnTabChanged;

            _deviceTabController.ChangeTabToFirst(true);
            _inputActionMapTabController.ChangeTabToFirst(true);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _deviceTabController.OnTabChanged -= OnTabChanged;
            _inputActionMapTabController.OnTabChanged -= OnTabChanged;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
            _inputSystemManager.SetInputActionAssetEnable(false);
#endif
        }

        private void RegisterUiEvents()
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        private void UnregisterUiEvents()
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        #region UI Event
        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }
        #endregion

        private void OnTabChanged(Tab changedTab)
        {
            if (changedTab is DeviceTab deviceTab)
            {
                OnDeviceTabChanged(deviceTab);
            }
            else if (changedTab is InputActionMapTab inputActionMapTab)
            {
                OnInputActionMapTabChanged(inputActionMapTab);
            }
        }

        private void OnDeviceTabChanged(DeviceTab changedTab)
        {
            if (!changedTab.IsOn)
            {
                SetActiveCurrentInputActionPage(false);
            }
            else
            {
                _currentDeviceTab = changedTab;

                SetActiveCurrentInputActionPage(true);
            }
        }

        private void OnInputActionMapTabChanged(InputActionMapTab changedTab)
        {
            if (!changedTab.IsOn)
            {
                SetActiveCurrentInputActionPage(false);
            }
            else
            {
                _currentInputActionMapTab = changedTab;

                SetActiveCurrentInputActionPage(true);
            }
        }

        private void SetActiveCurrentInputActionPage(bool active)
        {
            if (_currentDeviceTab == null || _currentInputActionMapTab == null)
            {
                return;
            }

            _inputActionPages.GetValueOrDefault(_currentDeviceTab.Device.name)?
                .GetValueOrDefault(_currentInputActionMapTab.InputActionMap.name)?
                .gameObject.SetActive(active);
        }
    }
}
