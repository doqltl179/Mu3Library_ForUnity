#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mu3Library.IS
{
    public class InputSystemManager : IInputSystemManager, IInputSystemManagerEventBus
    {
        private readonly Dictionary<string, InputActionAsset> _inputActionAssets = new();
        public int InputActionAssetCount => _inputActionAssets.Count;

        private readonly Dictionary<string, InputActionMap> _inputActionMaps = new();
        private readonly Dictionary<string, InputAction> _inputActions = new();



        public string GetOverrideJsonOfInputAction(string actionId)
        {
            var action = GetInputAction(actionId);
            if (action == null)
            {
                return null;
            }

            return action.SaveBindingOverridesAsJson();
        }

        public string GetOverrideJsonOfInputActionWithName(string actionMapName, string actionName)
            => GetOverrideJsonOfInputActionWithName("Default", actionMapName, actionName);

        public string GetOverrideJsonOfInputActionWithName(string assetId, string actionMapName, string actionName)
        {
            var action = GetInputActionWithName(assetId, actionMapName, actionName);
            if (action == null)
            {
                return null;
            }

            return action.SaveBindingOverridesAsJson();
        }

        public void ApplyInputActionBindingOverrideFromJson(string actionId, string actionJson)
        {
            if (string.IsNullOrEmpty(actionJson))
            {
                Debug.LogError("InputAction binding override json can not be null or empty.");
                return;
            }

            var action = GetInputAction(actionId);
            if (action == null)
            {
                return;
            }

            action.LoadBindingOverridesFromJson(actionJson);
        }

        public void ApplyInputActionBindingOverrideFromJsonWithName(string actionMapName, string actionName, string actionJson)
            => ApplyInputActionBindingOverrideFromJsonWithName("Default", actionMapName, actionName, actionJson);

        public void ApplyInputActionBindingOverrideFromJsonWithName(string assetId, string actionMapName, string actionName, string actionJson)
        {
            if (string.IsNullOrEmpty(actionJson))
            {
                Debug.LogError("InputAction binding override json can not be null or empty.");
                return;
            }

            var action = GetInputActionWithName(assetId, actionMapName, actionName);
            if (action == null)
            {
                return;
            }

            action.LoadBindingOverridesFromJson(actionJson);
        }

        public string GetOverrideJsonOfInputActionMap(string actionMapId)
        {
            var actionMap = GetInputActionMap(actionMapId);
            if (actionMap == null)
            {
                return null;
            }

            return actionMap.SaveBindingOverridesAsJson();
        }

        public string GetOverrideJsonOfInputActionMapWithName(string actionMapName)
            => GetOverrideJsonOfInputActionMapWithName("Default", actionMapName);

        public string GetOverrideJsonOfInputActionMapWithName(string assetId, string actionMapName)
        {
            var actionMap = GetInputActionMapWithName(assetId, actionMapName);
            if (actionMap == null)
            {
                return null;
            }

            return actionMap.SaveBindingOverridesAsJson();
        }

        public string GetJsonOfInputActionMap(string actionMapId)
        {
            var actionMap = GetInputActionMap(actionMapId);
            if (actionMap == null)
            {
                return null;
            }

            return actionMap.ToJson();
        }

        public string GetJsonOfInputActionMapWithName(string actionMapName)
            => GetJsonOfInputActionMapWithName("Default", actionMapName);

        public string GetJsonOfInputActionMapWithName(string assetId, string actionMapName)
        {
            var actionMap = GetInputActionMapWithName(assetId, actionMapName);
            if (actionMap == null)
            {
                return null;
            }

            return actionMap.ToJson();
        }

        public void ApplyInputActionMapBindingOverrideFromJson(string actionMapId, string actionMapJson)
        {
            if (string.IsNullOrEmpty(actionMapJson))
            {
                Debug.LogError("InputActionMap binding override json can not be null or empty.");
                return;
            }

            var actionMap = GetInputActionMap(actionMapId);
            if (actionMap == null)
            {
                return;
            }

            actionMap.LoadBindingOverridesFromJson(actionMapJson);
        }

        public void ApplyInputActionMapBindingOverrideFromJsonWithName(string actionMapName, string actionMapJson)
            => ApplyInputActionMapBindingOverrideFromJsonWithName("Default", actionMapName, actionMapJson);

        public void ApplyInputActionMapBindingOverrideFromJsonWithName(string assetId, string actionMapName, string actionMapJson)
        {
            if (string.IsNullOrEmpty(actionMapJson))
            {
                Debug.LogError("InputActionMap binding override json can not be null or empty.");
                return;
            }

            var actionMap = GetInputActionMapWithName(assetId, actionMapName);
            if (actionMap == null)
            {
                return;
            }

            actionMap.LoadBindingOverridesFromJson(actionMapJson);
        }

        public string GetOverrideJsonOfInputActionAsset()
            => GetOverrideJsonOfInputActionAsset("Default");

        public string GetOverrideJsonOfInputActionAsset(string assetId)
        {
            if (!IsValidInputActionAssetId(assetId) ||
                !_inputActionAssets.TryGetValue(assetId, out var asset) ||
                asset == null)
            {
                return null;
            }

            return asset.SaveBindingOverridesAsJson();
        }

        public string GetJsonOfInputActionAsset()
            => GetJsonOfInputActionAsset("Default");

        public string GetJsonOfInputActionAsset(string assetId)
        {
            if (!IsValidInputActionAssetId(assetId) ||
                !_inputActionAssets.TryGetValue(assetId, out var asset) ||
                asset == null)
            {
                return null;
            }

            return asset.ToJson();
        }

        public void ApplyInputActionAssetBindingOverrideFromJson(string assetJson)
            => ApplyInputActionAssetBindingOverrideFromJson("Default", assetJson);

        public void ApplyInputActionAssetBindingOverrideFromJson(string assetId, string assetJson)
        {
            if (string.IsNullOrEmpty(assetJson))
            {
                Debug.LogError("InputActionAsset binding override json can not be null or empty.");
                return;
            }

            if (!IsValidInputActionAssetId(assetId) ||
                !_inputActionAssets.TryGetValue(assetId, out var asset) ||
                asset == null)
            {
                return;
            }

            asset.LoadBindingOverridesFromJson(assetJson);
        }

        public void AddInputActionAssetFromJson(string assetJson)
            => AddInputActionAssetFromJson("Default", assetJson, false);

        public void AddInputActionAssetFromJson(string assetJson, bool enable)
            => AddInputActionAssetFromJson("Default", assetJson, enable);

        public void AddInputActionAssetFromJson(string assetId, string assetJson)
            => AddInputActionAssetFromJson(assetId, assetJson, false);

        public void AddInputActionAssetFromJson(string assetId, string assetJson, bool enable)
        {
            if (string.IsNullOrEmpty(assetJson))
            {
                Debug.LogError("InputActionAsset json can not be null or empty.");
                return;
            }

            try
            {
                var asset = InputActionAsset.FromJson(assetJson);
                AddInputActionAsset(asset, assetId, enable);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to parse InputActionAsset from json. Exception: {ex}");
            }
        }

        public InputAction GetInputAction(string actionId)
        {
            if (!IsValidInputActionId(actionId))
            {
                return null;
            }

            return _inputActions.GetValueOrDefault(actionId);
        }

        public InputAction GetInputActionWithName(string actionMapName, string actionName)
            => GetInputActionWithName("Default", actionMapName, actionName);

        public InputAction GetInputActionWithName(string assetId, string actionMapName, string actionName)
        {
            if (!IsValidInputActionAssetId(assetId) ||
                string.IsNullOrEmpty(actionMapName) ||
                string.IsNullOrEmpty(actionName) ||
                !_inputActionAssets.TryGetValue(assetId, out var asset))
            {
                return null;
            }

            return asset.FindActionMap(actionMapName)?.FindAction(actionName);
        }

        public InputActionMap GetInputActionMap(string actionMapId)
        {
            if (!IsValidInputActionMapId(actionMapId))
            {
                return null;
            }

            return _inputActionMaps.GetValueOrDefault(actionMapId);
        }

        public InputActionMap GetInputActionMapWithName(string actionMapName)
            => GetInputActionMapWithName("Default", actionMapName);

        public InputActionMap GetInputActionMapWithName(string assetId, string actionMapName)
        {
            if (!IsValidInputActionAssetId(assetId) ||
                string.IsNullOrEmpty(actionMapName) ||
                !_inputActionAssets.TryGetValue(assetId, out var asset))
            {
                return null;
            }

            return asset.FindActionMap(actionMapName);
        }

        public InputActionAsset GetInputActionAsset()
            => GetInputActionAsset("Default");

        public InputActionAsset GetInputActionAsset(string assetId)
        {
            if (!IsValidInputActionAssetId(assetId))
            {
                return null;
            }

            return _inputActionAssets.GetValueOrDefault(assetId);
        }

        public void SetInputActionAssetEnable(bool enable)
            => SetInputActionAssetEnable("Default", enable);

        public void SetInputActionAssetEnable(string assetId, bool enable)
        {
            if (!IsValidInputActionAssetId(assetId) ||
                !_inputActionAssets.TryGetValue(assetId, out var asset) ||
                asset == null)
            {
                return;
            }

            if (enable)
            {
                Debug.Log($"InputActionAsset enabled. id: {assetId}");

                asset.Enable();
            }
            else
            {
                Debug.Log($"InputActionAsset disabled. id: {assetId}");

                asset.Disable();
            }
        }

        public void AddInputActionAsset(InputActionAsset asset)
            => AddInputActionAsset(asset, "Default", false);

        public void AddInputActionAsset(InputActionAsset asset, bool enable)
            => AddInputActionAsset(asset, "Default", enable);

        public void AddInputActionAsset(InputActionAsset asset, string assetId)
            => AddInputActionAsset(asset, assetId, false);

        public void AddInputActionAsset(InputActionAsset asset, string assetId, bool enable)
        {
            if (_inputActionAssets.ContainsKey(assetId))
            {
                Debug.LogWarning($"InputActionAsset with id '{assetId}' already exists. It will be replaced.");
            }

            if (AddInputActionAssetToDictionary(asset, assetId))
            {
                if (enable)
                {
                    asset.Enable();
                }
            }
        }

        private bool RemoveInputActionFromDictionary(InputAction action)
            => RemoveInputActionFromDictionary(action?.id.ToString());

        private bool RemoveInputActionFromDictionary(string actionId)
        {
            if (!IsValidInputActionId(actionId) ||
               !_inputActions.TryGetValue(actionId, out var oldAction) ||
               oldAction == null)
            {
                return false;
            }

            _inputActions.Remove(actionId);

            return true;
        }

        private bool RemoveInputActionMapFromDictionary(InputActionMap actionMap)
            => RemoveInputActionMapFromDictionary(actionMap?.id.ToString());

        private bool RemoveInputActionMapFromDictionary(string actionMapId)
        {
            if (!IsValidInputActionMapId(actionMapId) ||
               !_inputActionMaps.TryGetValue(actionMapId, out var oldMap) ||
               oldMap == null)
            {
                return false;
            }

            _inputActionMaps.Remove(actionMapId);

            foreach (var action in oldMap.actions)
            {
                RemoveInputActionFromDictionary(action);
            }

            return true;
        }

        private bool RemoveInputActionAssetFromDictionary(string assetId)
        {
            if (!IsValidInputActionAssetId(assetId) ||
               !_inputActionAssets.TryGetValue(assetId, out var oldAsset) ||
               oldAsset == null)
            {
                return false;
            }

            _inputActionAssets.Remove(assetId);

            foreach (var actionMap in oldAsset.actionMaps)
            {
                RemoveInputActionMapFromDictionary(actionMap);
            }

            return true;
        }

        private bool AddInputActionToDictionary(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            string actionId = action.id.ToString();
            _inputActions[actionId] = action;

            return true;
        }

        private bool AddInputActionMapToDictionary(InputActionMap actionMap)
        {
            if (actionMap == null)
            {
                return false;
            }

            RemoveInputActionMapFromDictionary(actionMap);

            string actionMapId = actionMap.id.ToString();
            _inputActionMaps[actionMapId] = actionMap;

            foreach (var action in actionMap.actions)
            {
                AddInputActionToDictionary(action);
            }

            return true;
        }

        private bool AddInputActionAssetToDictionary(InputActionAsset asset, string assetId)
        {
            if (asset == null ||
               !IsValidInputActionAssetId(assetId))
            {
                return false;
            }

            RemoveInputActionAssetFromDictionary(assetId);

            _inputActionAssets[assetId] = asset;

            foreach (var actionMap in asset.actionMaps)
            {
                AddInputActionMapToDictionary(actionMap);
            }

            return true;
        }

        private bool IsValidInputActionId(string inputActionId)
        {
            if (string.IsNullOrEmpty(inputActionId))
            {
                Debug.LogError("InputAction id can not be null or empty.");
                return false;
            }

            return true;
        }

        private bool IsValidInputActionMapId(string inputActionMapId)
        {
            if (string.IsNullOrEmpty(inputActionMapId))
            {
                Debug.LogError("InputActionMap id can not be null or empty.");
                return false;
            }

            return true;
        }

        private bool IsValidInputActionAssetId(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
            {
                Debug.LogError("InputActionAsset id can not be null or empty.");
                return false;
            }

            return true;
        }
    }
}
#endif