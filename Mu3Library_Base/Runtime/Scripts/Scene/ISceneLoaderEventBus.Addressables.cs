#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;

namespace Mu3Library.Scene
{
    public readonly struct SceneLoadFailedInfo
    {
        /// <summary>The Addressables key the failed load was started with.</summary>
        public string Target { get; }
        public bool IsAdditive { get; }
        /// <summary>The message of the exception Addressables failed the load with, or empty when it gave none.</summary>
        public string ErrorMessage { get; }

        public SceneLoadFailedInfo(string target, bool isAdditive, string errorMessage)
        {
            Target = target;
            IsAdditive = isAdditive;
            ErrorMessage = errorMessage;
        }
    }

    public partial interface ISceneLoaderEventBus
    {
        /// <summary>
        /// Raised when an Addressables single or additive scene load that had started fails, for
        /// <br/> example for a key no catalog holds or a scene that cannot be downloaded. That load
        /// <br/> raises no loaded event, and by the time this is raised its command is over:
        /// <br/> <see cref="ISceneLoader.IsLoading"/> and the status queries no longer count it,
        /// <br/> so a handler can start the next load right away.
        /// </summary>
        public event Action<SceneLoadFailedInfo> OnSceneLoadFailed;
    }
}
#endif
