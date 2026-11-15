#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using System;
using UnityEngine.InputSystem;

namespace Mu3Library.IS
{
    public interface IInputSystemManagerEventBus
    {
        /// <summary>
        /// Raised with the asset id and the asset after it was added to or replaced the
        /// registry under that id.
        /// </summary>
        event Action<string, InputActionAsset> OnAssetAdded;

        /// <summary>
        /// Raised with the asset id and the asset that left the registry, before an asset the
        /// manager built itself is destroyed.
        /// </summary>
        event Action<string, InputActionAsset> OnAssetRemoved;

        /// <summary>
        /// Raised with the action and the binding index when an interactive rebind starts.
        /// </summary>
        event Action<InputAction, int> OnRebindStarted;

        /// <summary>
        /// Raised with the action and the binding index when an interactive rebind completed.
        /// </summary>
        event Action<InputAction, int> OnRebindCompleted;

        /// <summary>
        /// Raised with the action and the binding index when an interactive rebind was canceled.
        /// </summary>
        event Action<InputAction, int> OnRebindCanceled;
    }
}
#endif
