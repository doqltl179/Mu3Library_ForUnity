#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Mu3Library.Addressable
{
    public partial interface IAddressablesManager
    {
        public bool IsInitialized { get; }

        public event Action<float> OnInitializeProgress;
        public event Action<float> OnDownloadProgress;

        public void Initialize(Action callback = null);

        public void LoadAssetAsync<T>(object key, Action<T> callback = null) where T : class;
        public void LoadAssetsAsync<T>(object key, Action<IList<T>> callback = null, Action<T> perAssetCallback = null);

        public void GetDownloadSizeAsync(object key, Action<long> callback = null);
        public void GetDownloadSizeAsync(IEnumerable keys, Action<long> callback = null, Addressables.MergeMode mergeMode = Addressables.MergeMode.Union);
        public void DownloadDependenciesAsync(object key, Action callback = null, Action<float> progress = null, bool autoReleaseHandle = true);
        public void DownloadDependenciesAsync(IEnumerable keys, Action callback = null, Action<float> progress = null, bool autoReleaseHandle = true, Addressables.MergeMode mergeMode = Addressables.MergeMode.Union);

        public void CheckForCatalogUpdates(Action<IList<string>> callback = null, bool autoReleaseHandle = true);
        public void UpdateCatalogs(Action<IList<IResourceLocator>> callback = null, bool autoReleaseHandle = true);

        public bool TryGetCachedAsset<T>(object key, out T asset) where T : class;
        public void ReleaseCachedAsset(object key);
        public void ClearCache();
    }
}

#endif
