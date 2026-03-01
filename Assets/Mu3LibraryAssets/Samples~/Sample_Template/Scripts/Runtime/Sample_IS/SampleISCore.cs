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
        private const string InputActionAssetResourcePath = "Sample_IS/InputSystemActions";

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
        [SerializeField] private MessageHandler _messageHandler;

        [Space(20)]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Toggle _autoSaveToggle;

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
            InputActionAsset inputActionAsset = _resourceLoader.Load<InputActionAsset>(InputActionAssetResourcePath);
            _inputSystemManager.AddInputActionAsset(inputActionAsset);

            string overrideData = DataCacheAgent.Load("Default");
            if (!string.IsNullOrEmpty(overrideData))
            {
                _inputSystemManager.ApplyInputActionAssetBindingOverrideFromJson(overrideData);
            }
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

                    if (inputActionPage.InputActionItemCount == 0)
                    {
                        Destroy(inputActionPage.gameObject);
                        continue;
                    }

                    inputActionPage.OnBindingItemClicked += OnBindingItemClicked;

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

            _resourceLoader.Release<InputActionAsset>(InputActionAssetResourcePath);
        }

        private void RegisterUiEvents()
        {
            _saveButton.onClick.AddListener(OnSaveButtonClicked);
            _resetButton.onClick.AddListener(OnResetButtonClicked);
            _autoSaveToggle.onValueChanged.AddListener(OnAutoSaveToggleValueChanged);
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        private void UnregisterUiEvents()
        {
            _saveButton.onClick.RemoveListener(OnSaveButtonClicked);
            _resetButton.onClick.RemoveListener(OnResetButtonClicked);
            _autoSaveToggle.onValueChanged.RemoveListener(OnAutoSaveToggleValueChanged);
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        #region UI Event
        private void OnAutoSaveToggleValueChanged(bool isOn)
        {
            // Nothing.
        }

        private void OnSaveButtonClicked()
        {
            SaveOverrideData();
        }

        private void OnResetButtonClicked()
        {
            RemoveOverrideData();

            foreach (var devicePages in _inputActionPages.Values)
            {
                foreach (var page in devicePages.Values)
                {
                    page?.Patch();
                }
            }
        }

        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }
        #endregion

        private void OnBindingItemClicked(InputActionItem inputActionItem, InputActionBindingItem bindingItem)
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
            int bindingIndex = -1;
            var bindings = inputActionItem.InputAction.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].id == bindingItem.InputBinding.id)
                {
                    bindingIndex = i;
                    break;
                }
            }

            if (bindingIndex < 0)
            {
                Debug.LogWarning($"Binding not found for action '{inputActionItem.InputAction.name}'.");
                return;
            }

            var cancelAction = _inputSystemManager.GetInputAction(InputActionAssetKeys.UI.Cancel.Id);

            var rebindOperation = _inputSystemManager.StartInteractiveRebind(
                inputActionItem.InputAction,
                bindingIndex,
                targetDeviceTypes: inputActionItem.Device != null ? new[] { inputActionItem.Device.GetType() } : null,
                cancellingThroughControls: cancelAction?.controls.ToArray(),
                onComplete: () =>
                {
                    bindingItem.SetInputBinding(inputActionItem.InputAction.bindings[bindingIndex]);

                    if (_autoSaveToggle.isOn)
                    {
                        SaveOverrideData();
                    }
                },
                onCancel: () =>
                {

                },
                onFinally: () =>
                {
                    _messageHandler.HideMessage();
                }
            );

            System.Text.StringBuilder messageSb = new System.Text.StringBuilder();
            messageSb.Append($"[{inputActionItem.InputAction.name}]");
            if (!string.IsNullOrEmpty(bindingItem.InputBinding.effectivePath))
            {
                string nameString = InputControlPath.ToHumanReadableString(
                    bindingItem.InputBinding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
                messageSb.Append($" - [{nameString}]");
            }
            messageSb.AppendLine();
            messageSb.AppendLine("<size=80%>Input New Key</size>");
            _messageHandler.ShowMessage(
                messageSb.ToString(),
                () =>
                {
                    rebindOperation?.Cancel();
                }
            );
#endif
        }

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

        private void SaveOverrideData()
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
            string overrideData = _inputSystemManager.GetOverrideJsonOfInputActionAsset();

            DataCacheAgent.Save("Default", overrideData);
#endif
        }

        private void RemoveOverrideData()
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
            _inputSystemManager.RemoveAllInputActionAssetBindingOverrides();

            DataCacheAgent.Remove("Default");
#endif
        }
    }
}
