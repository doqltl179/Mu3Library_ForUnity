#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;

namespace Mu3Library.IS
{
    public interface IInputSystemManager
    {
        public int InputActionAssetCount { get; }



        public string GetOverrideJsonOfInputAction(string actionId);
        public string GetOverrideJsonOfInputActionWithName(string actionMapName, string actionName);
        public string GetOverrideJsonOfInputActionWithName(string assetId, string actionMapName, string actionName);

        public void ApplyInputActionBindingOverrideFromJson(string actionId, string actionJson);
        public void ApplyInputActionBindingOverrideFromJsonWithName(string actionMapName, string actionName, string actionJson);
        public void ApplyInputActionBindingOverrideFromJsonWithName(string assetId, string actionMapName, string actionName, string actionJson);

        public string GetOverrideJsonOfInputActionMap(string actionMapId);
        public string GetOverrideJsonOfInputActionMapWithName(string actionMapName);
        public string GetOverrideJsonOfInputActionMapWithName(string assetId, string actionMapName);

        public string GetJsonOfInputActionMap(string actionMapId);
        public string GetJsonOfInputActionMapWithName(string actionMapName);
        public string GetJsonOfInputActionMapWithName(string assetId, string actionMapName);

        public void ApplyInputActionMapBindingOverrideFromJson(string actionMapId, string actionMapJson);
        public void ApplyInputActionMapBindingOverrideFromJsonWithName(string actionMapName, string actionMapJson);
        public void ApplyInputActionMapBindingOverrideFromJsonWithName(string assetId, string actionMapName, string actionMapJson);

        public string GetOverrideJsonOfInputActionAsset();
        public string GetOverrideJsonOfInputActionAsset(string assetId);

        public string GetJsonOfInputActionAsset();
        public string GetJsonOfInputActionAsset(string assetId);

        public void ApplyInputActionAssetBindingOverrideFromJson(string assetJson);
        public void ApplyInputActionAssetBindingOverrideFromJson(string assetId, string assetJson);

        public void AddInputActionAssetFromJson(string assetJson);
        public void AddInputActionAssetFromJson(string assetJson, bool enable);
        public void AddInputActionAssetFromJson(string assetId, string assetJson);
        public void AddInputActionAssetFromJson(string assetId, string assetJson, bool enable);

        public InputAction GetInputAction(string actionId);
        public InputAction GetInputActionWithName(string actionMapName, string actionName);
        public InputAction GetInputActionWithName(string assetId, string actionMapName, string actionName);

        public InputActionMap GetInputActionMap(string actionMapId);
        public InputActionMap GetInputActionMapWithName(string actionMapName);
        public InputActionMap GetInputActionMapWithName(string assetId, string actionMapName);

        public InputActionAsset GetInputActionAsset();
        public InputActionAsset GetInputActionAsset(string assetId);

        public void SetInputActionAssetEnable(bool enable);
        public void SetInputActionAssetEnable(string assetId, bool enable);

        public void AddInputActionAsset(InputActionAsset asset);
        public void AddInputActionAsset(InputActionAsset asset, bool enable);
        public void AddInputActionAsset(InputActionAsset asset, string assetId);
        public void AddInputActionAsset(InputActionAsset asset, string assetId, bool enable);
    }
}
#endif