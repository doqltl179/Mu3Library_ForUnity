using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Addressable
{
    public partial class AddressablesManager
    {
        /// <summary>How many handles of one cache entry are out, and which entry they belong to.</summary>
        private sealed class HandleCount
        {
            public int Count;
            public int Generation;
        }

        private readonly Dictionary<object, HandleCount> _handleCounts = new();
        private int _lastHandleGeneration;



        public void LoadAssetHandle<T>(object key, Action<AssetHandle<T>> callback) where T : class
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "A handle must reach a caller that disposes it.");
            }

            object cacheKey = CreateSingleAssetCacheKey<T>(key);
            LoadAsset<T>(key, asset => callback(CreateHandle(key, cacheKey, asset)));
        }

        /// <summary>Number of handles out for the key and type, for diagnostics and tests.</summary>
        public int GetHandleCount<T>(object key) where T : class
        {
            return _handleCounts.TryGetValue(CreateSingleAssetCacheKey<T>(key), out HandleCount count) ? count.Count : 0;
        }

        internal bool IsHandleCurrent(object cacheKey, int generation)
        {
            return _handleCounts.TryGetValue(cacheKey, out HandleCount count) && count.Generation == generation;
        }

        internal void ReleaseHandle(object baseKey, object cacheKey, int generation)
        {
            if (!_handleCounts.TryGetValue(cacheKey, out HandleCount count) || count.Generation != generation)
            {
                return;
            }

            count.Count--;
            if (count.Count > 0)
            {
                return;
            }

            _handleCounts.Remove(cacheKey);
            ReleaseCacheEntry(baseKey, cacheKey);
        }

        // A handle counts only while its asset is the one the cache holds, so the last handle's
        // release frees exactly what the handles shared.
        private AssetHandle<T> CreateHandle<T>(object key, object cacheKey, T asset) where T : class
        {
            if (asset == null || !TryGetCachedAssetInternal(cacheKey, out T cached) || !ReferenceEquals(cached, asset))
            {
                return new AssetHandle<T>(key);
            }

            if (!_handleCounts.TryGetValue(cacheKey, out HandleCount count))
            {
                count = new HandleCount { Generation = ++_lastHandleGeneration };
                _handleCounts.Add(cacheKey, count);
            }

            count.Count++;
            return new AssetHandle<T>(this, key, cacheKey, count.Generation, asset);
        }

        private void ReleaseCacheEntry(object baseKey, object cacheKey)
        {
            if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                _assetHandleCache.Remove(cacheKey);
            }

            if (_assetCache.TryGetValue(cacheKey, out object cachedAsset))
            {
                _cachedAssetKeyMap.Remove(cachedAsset);
                _assetCache.Remove(cacheKey);
            }

            UntrackCacheKey(baseKey, cacheKey);
        }
    }
}
