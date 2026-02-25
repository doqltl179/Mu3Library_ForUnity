#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mu3Library.IS
{
    public class InputSystemManager : IInputSystemManager, IInputSystemManagerEventBus
    {
        protected readonly Dictionary<string, InputActionAsset> _inputActionAssets = new();
        public int InputActionAssetCount => _inputActionAssets.Count;

        protected readonly Dictionary<string, Dictionary<string, InputActionMap>> _inputActionMaps = new();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, InputAction>>> _inputActions = new();



        public InputAction GetInputAction(string actionMapName, string actionName)
            => GetInputAction("Default", actionMapName, actionName);

        public InputAction GetInputAction(string assetId, string actionMapName, string actionName)
        {
            if (!IsValidId(assetId) ||
                !IsValidInputActionMapName(actionMapName) ||
                !IsValidInputActionName(actionName))
            {
                return null;
            }

            return _inputActions.GetValueOrDefault(assetId)?
                .GetValueOrDefault(actionMapName)?
                .GetValueOrDefault(actionName);
        }

        public InputActionMap GetInputActionMap(string actionMapName)
            => GetInputActionMap("Default", actionMapName);

        public InputActionMap GetInputActionMap(string assetId, string actionMapName)
        {
            if (!IsValidId(assetId) ||
                !IsValidInputActionMapName(actionMapName))
            {
                return null;
            }

            return _inputActionMaps.GetValueOrDefault(assetId)?
                .GetValueOrDefault(actionMapName);
        }

        public InputActionAsset GetInputActionAsset()
            => GetInputActionAsset("Default");

        public InputActionAsset GetInputActionAsset(string id)
        {
            if (!IsValidId(id))
            {
                return null;
            }

            return _inputActionAssets.GetValueOrDefault(id);
        }

        public void SetInputActionAssetEnable(bool enable)
            => SetInputActionAssetEnable("Default", enable);

        public void SetInputActionAssetEnable(string id, bool enable)
        {
            if (!IsValidId(id) ||
                !_inputActionAssets.TryGetValue(id, out var asset) ||
                asset == null)
            {
                return;
            }

            if (enable)
            {
                Debug.Log($"InputActionAsset enabled. id: {id}");

                asset.Enable();
            }
            else
            {
                Debug.Log($"InputActionAsset disabled. id: {id}");

                asset.Disable();
            }
        }

        public void AddInputActionAsset(InputActionAsset asset)
            => AddInputActionAsset(asset, "Default", false);

        public void AddInputActionAsset(InputActionAsset asset, bool enable)
            => AddInputActionAsset(asset, "Default", enable);

        public void AddInputActionAsset(InputActionAsset asset, string id)
            => AddInputActionAsset(asset, id, false);

        public void AddInputActionAsset(InputActionAsset asset, string id, bool enable)
        {
            if (!IsValidId(id))
            {
                return;
            }

            if (_inputActionAssets.ContainsKey(id))
            {
                Debug.LogWarning($"InputActionAsset with id '{id}' already exists. It will be replaced.");
            }

            if (asset != null)
            {
                _inputActionAssets[id] = asset;

                var actionMaps = new Dictionary<string, InputActionMap>();
                foreach (var actionMap in asset.actionMaps)
                {
                    string actionMapName = actionMap.name;
                    actionMaps[actionMapName] = actionMap;

                    var actions = new Dictionary<string, Dictionary<string, InputAction>>();
                    foreach (var action in actionMap.actions)
                    {
                        string actionName = action.name;
                        actions[actionMapName][actionName] = action;
                    }

                    _inputActions[id] = actions;
                }

                _inputActionMaps[id] = actionMaps;

                SetInputActionAssetEnable(id, enable);
            }
            else
            {
                _inputActionAssets.Remove(id);
                _inputActionMaps.Remove(id);
                _inputActions.Remove(id);
            }
        }

        private bool IsValidInputActionName(string inputActionName)
        {
            if (string.IsNullOrEmpty(inputActionName))
            {
                Debug.LogError("InputAction name can not be null or empty.");
                return false;
            }

            return true;
        }

        private bool IsValidInputActionMapName(string inputActionMapName)
        {
            if (string.IsNullOrEmpty(inputActionMapName))
            {
                Debug.LogError("InputActionMap name can not be null or empty.");
                return false;
            }

            return true;
        }

        private bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("id can not be null or empty.");
                return false;
            }

            return true;
        }
    }
}
#endif