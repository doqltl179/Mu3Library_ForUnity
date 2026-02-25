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

        public InputActionAsset GetInputActionAsset(string id)
        {
            if (!IsValidInputActionAssetId(id))
            {
                return null;
            }

            return _inputActionAssets.GetValueOrDefault(id);
        }

        public void SetInputActionAssetEnable(bool enable)
            => SetInputActionAssetEnable("Default", enable);

        public void SetInputActionAssetEnable(string id, bool enable)
        {
            if (!IsValidInputActionAssetId(id) ||
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
            if (!IsValidInputActionAssetId(id))
            {
                return;
            }

            if (_inputActionAssets.ContainsKey(id))
            {
                Debug.LogWarning($"InputActionAsset with id '{id}' already exists. It will be replaced.");
            }

            if (asset != null)
            {
                // 기존 asset이 있으면 관련 map/action 항목을 먼저 제거
                if (_inputActionAssets.TryGetValue(id, out var existing))
                {
                    foreach (var actionMap in existing.actionMaps)
                    {
                        _inputActionMaps.Remove(actionMap.id.ToString());
                        foreach (var action in actionMap.actions)
                        {
                            _inputActions.Remove(action.id.ToString());
                        }
                    }
                }

                _inputActionAssets[id] = asset;

                foreach (var actionMap in asset.actionMaps)
                {
                    string actionMapId = actionMap.id.ToString();
                    _inputActionMaps[actionMapId] = actionMap;

                    foreach (var action in actionMap.actions)
                    {
                        string actionId = action.id.ToString();
                        _inputActions[actionId] = action;
                    }
                }

                SetInputActionAssetEnable(id, enable);
            }
            else
            {
                if (_inputActionAssets.TryGetValue(id, out var toRemove))
                {
                    foreach (var actionMap in toRemove.actionMaps)
                    {
                        _inputActionMaps.Remove(actionMap.id.ToString());
                        foreach (var action in actionMap.actions)
                        {
                            _inputActions.Remove(action.id.ToString());
                        }
                    }
                }

                _inputActionAssets.Remove(id);
            }
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

        private bool IsValidInputActionAssetId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("InputActionAsset id can not be null or empty.");
                return false;
            }

            return true;
        }
    }
}
#endif