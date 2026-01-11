#if MU3LIBRARY_ADDRESSABLES_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Mu3Library.Addressable
{
    public partial interface IAddressablesManager
    {
        public UniTask<T> LoadAssetAsync<T>(object key) where T : class;
        public UniTask<IList<T>> LoadAssetsAsync<T>(object key, Action<T> perAssetCallback = null);

        public UniTask<long> GetDownloadSizeAsync(object key);
        public UniTask<long> GetDownloadSizeAsync(IEnumerable keys, Addressables.MergeMode mergeMode = Addressables.MergeMode.Union);

        public UniTask<IList<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true);
        public UniTask<IList<IResourceLocator>> UpdateCatalogs(bool autoReleaseHandle = true);

        public UniTask DownloadDependenciesAsync(object key, Action<float> progress = null, bool autoReleaseHandle = true);
        public UniTask DownloadDependenciesAsync(IEnumerable keys, Action<float> progress = null, bool autoReleaseHandle = true, Addressables.MergeMode mergeMode = Addressables.MergeMode.Union);
    }
}
#endif
