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
        public bool IsInitializing { get; }
        public string InitializeError { get; }

        public void Initialize(Action callback = null);
        public void InitializeWithResult(Action<bool, string> callback);

        public void LoadAsset<T>(object key, Action<T> callback = null) where T : class;

        /// <summary>
        /// Loads the asset as <see cref="LoadAsset{T}"/> does and hands the caller its own
        /// <see cref="AssetHandle{T}"/>. The manager counts the handles of each key and type and
        /// releases the cached asset when the last one is disposed, so callers that share an asset
        /// keep it for one another. A call that joins a load in progress gets its handle when the load
        /// completes; a load that fails hands a handle that holds no asset and is not valid.
        /// </summary>
        public void LoadAssetHandle<T>(object key, Action<AssetHandle<T>> callback) where T : class;
        public void LoadAssets<T>(object key, Action<T> perAssetCallback = null, Action<IList<T>> callback = null);
        public void LoadAssetsWithKeys<T>(object key, Action<Dictionary<string, T>> callback = null);

        public void GetDownloadSize(object key, Action<long> callback);
        public void GetDownloadSize(IEnumerable keys, Addressables.MergeMode mergeMode, Action<long> callback);

        public void DownloadDependencies(object key, Action<float> progress = null, Action callback = null, bool autoReleaseHandle = true);
        public void DownloadDependencies(IEnumerable keys, Addressables.MergeMode mergeMode, Action<float> progress = null, Action callback = null, bool autoReleaseHandle = true);

        public void CheckForCatalogUpdates(Action<IList<string>> callback, bool autoReleaseHandle = true);
        public void UpdateCatalogs(Action<IList<IResourceLocator>> callback, bool autoReleaseHandle = true);
        public void UpdateCatalogs(Action<IList<IResourceLocator>> callback, bool autoCleanBundleCache, bool autoReleaseHandle);

        public bool IsKeyExist(object key);

        public string GetCachedAddress(object asset);
        public T GetCachedAddress<T>(object asset) where T : class;

        public bool TryGetCachedAsset<T>(object key, out T asset) where T : class;
        /// <summary>
        /// Releases everything the key loaded, for every caller at once: the cache counts no caller of
        /// <see cref="LoadAsset{T}"/>, and the handles of <see cref="LoadAssetHandle{T}"/> that remain
        /// are no longer valid.
        /// </summary>
        public void ReleaseCachedAsset(object key);

        /// <summary>Releases every cached asset, as <see cref="ReleaseCachedAsset"/> does for each key.</summary>
        public void ClearCache();
    }
}
