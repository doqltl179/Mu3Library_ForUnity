#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;

namespace Mu3Library.IS
{
    public interface IInputSystemManager
    {
        public int InputActionAssetCount { get; }



        public InputAction GetInputAction(string actionId);
        public InputAction GetInputActionWithName(string actionMapName, string actionName);
        public InputAction GetInputActionWithName(string assetId, string actionMapName, string actionName);

        public InputActionMap GetInputActionMap(string actionMapId);
        public InputActionMap GetInputActionMapWithName(string actionMapName);
        public InputActionMap GetInputActionMapWithName(string assetId, string actionMapName);

        public InputActionAsset GetInputActionAsset();
        public InputActionAsset GetInputActionAsset(string id);

        public void SetInputActionAssetEnable(bool enable);
        public void SetInputActionAssetEnable(string id, bool enable);

        public void AddInputActionAsset(InputActionAsset asset);
        public void AddInputActionAsset(InputActionAsset asset, bool enable);
        public void AddInputActionAsset(InputActionAsset asset, string id);
        public void AddInputActionAsset(InputActionAsset asset, string id, bool enable);
    }
}
#endif