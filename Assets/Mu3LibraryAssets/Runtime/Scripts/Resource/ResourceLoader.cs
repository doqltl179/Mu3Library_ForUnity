using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Resource
{
    public partial class ResourceLoader : IResourceLoader, IDisposable
    {
        private readonly Dictionary<string, Dictionary<Type, Object>> _resources = new();
        private readonly Dictionary<string, Dictionary<Type, Object[]>> _resourceListCache = new();



        public void Dispose()
        {
            ReleaseAll();
        }

        public bool IsCached<T>(string path) where T : Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = NormalizePath(path);
            return TryGetCached<T>(path, out _);
        }

        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("Resource path is null or empty.");
                return null;
            }

            path = NormalizePath(path);

            if (TryGetCached<T>(path, out Object cached))
            {
                return cached as T;
            }

            Object loaded = Resources.Load<T>(path);
            T result = loaded as T;

            if (result == null)
            {
                Debug.LogError($"Object not found in resources folder. path: {path}");
                return null;
            }

            Cache(path, result);
            return result;
        }

        public T[] LoadAll<T>(string path) where T : Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("Resource path is null or empty.");
                return Array.Empty<T>();
            }

            path = NormalizePath(path);

            if (TryGetCachedAll(path, out T[] cached))
            {
                return cached;
            }

            T[] loaded = Resources.LoadAll<T>(path);
            if (loaded == null || loaded.Length == 0)
            {
                return Array.Empty<T>();
            }

            CacheAll(path, loaded);
            return loaded;
        }

        public void LoadAsync<T>(string path, Action<T> onLoaded) where T : Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("Resource path is null or empty.");
                onLoaded?.Invoke(null);
                return;
            }

            path = NormalizePath(path);

            TryGetCached<T>(path, out Object cached);
            T result = cached as T;

            if (result != null)
            {
                onLoaded?.Invoke(result);
                return;
            }

            ResourceRequest request = Resources.LoadAsync<T>(path);
            request.completed += _ =>
            {
                Object loaded = request.asset;
                result = loaded as T;

                if (result == null)
                {
                    Debug.LogError($"Object not found in resources folder. path: {path}");
                    onLoaded?.Invoke(null);
                    return;
                }

                Cache(path, result);
                onLoaded?.Invoke(result);
            };
        }

        public bool TryLoad<T>(string path, out T asset) where T : Object
        {
            asset = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = NormalizePath(path);

            TryGetCached<T>(path, out Object cached);
            asset = cached as T;

            if (asset != null)
            {
                return true;
            }

            Object loaded = Resources.Load<T>(path);
            asset = loaded as T;

            if (asset == null)
            {
                return false;
            }

            Cache(path, asset);
            return true;
        }

        public bool Release<T>(string path) where T : Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = NormalizePath(path);
            Type type = typeof(T);
            bool released = false;

            if (_resources.TryGetValue(path, out Dictionary<Type, Object> pathResources) &&
                pathResources.TryGetValue(type, out Object asset))
            {
                pathResources.Remove(type);

                if (asset != null)
                {
                    Resources.UnloadAsset(asset);
                    released = true;
                }

                if (pathResources.Count == 0)
                {
                    _resources.Remove(path);
                }
            }

            if (_resourceListCache.TryGetValue(path, out Dictionary<Type, Object[]> listPathResources) &&
                listPathResources.TryGetValue(type, out Object[] listAssets))
            {
                listPathResources.Remove(type);

                if (listAssets != null)
                {
                    for (int i = 0; i < listAssets.Length; i++)
                    {
                        Object listAsset = listAssets[i];
                        if (listAsset != null)
                        {
                            Resources.UnloadAsset(listAsset);
                        }
                    }

                    released = true;
                }

                if (listPathResources.Count == 0)
                {
                    _resourceListCache.Remove(path);
                }
            }

            return released;
        }

        public void ReleaseAll(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("Resource path is null or empty.");
                return;
            }

            path = NormalizePath(path);

            bool hasResources = _resources.TryGetValue(path, out Dictionary<Type, Object> pathResources);
            bool hasListResources = _resourceListCache.TryGetValue(path, out Dictionary<Type, Object[]> listPathResources);
            if (!hasResources && !hasListResources)
            {
                Debug.LogWarning($"No cached resources found for path: {path}");
                return;
            }

            HashSet<Object> uniqueAssets = new HashSet<Object>();
            if (hasResources)
            {
                foreach (KeyValuePair<Type, Object> entry in pathResources)
                {
                    Object asset = entry.Value;
                    if (asset != null)
                    {
                        uniqueAssets.Add(asset);
                    }
                }
            }

            if (hasListResources)
            {
                foreach (KeyValuePair<Type, Object[]> entry in listPathResources)
                {
                    Object[] assets = entry.Value;
                    if (assets == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < assets.Length; i++)
                    {
                        Object asset = assets[i];
                        if (asset != null)
                        {
                            uniqueAssets.Add(asset);
                        }
                    }
                }
            }

            foreach (Object asset in uniqueAssets)
            {
                Resources.UnloadAsset(asset);
            }

            if (hasResources)
            {
                _resources.Remove(path);
            }

            if (hasListResources)
            {
                _resourceListCache.Remove(path);
            }
        }

        public void ReleaseAll()
        {
            HashSet<Object> uniqueAssets = new HashSet<Object>();
            foreach (Dictionary<Type, Object> entry in _resources.Values)
            {
                foreach (Object asset in entry.Values)
                {
                    if (asset != null)
                    {
                        uniqueAssets.Add(asset);
                    }
                }
            }

            foreach (Dictionary<Type, Object[]> entry in _resourceListCache.Values)
            {
                foreach (Object[] assets in entry.Values)
                {
                    if (assets == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < assets.Length; i++)
                    {
                        Object asset = assets[i];
                        if (asset != null)
                        {
                            uniqueAssets.Add(asset);
                        }
                    }
                }
            }

            foreach (Object asset in uniqueAssets)
            {
                Resources.UnloadAsset(asset);
            }

            _resources.Clear();
            _resourceListCache.Clear();
        }

        private bool TryGetCached<T>(string path, out Object cached) where T : Object
            => TryGetCached(path, typeof(T), out cached);

        private bool TryGetCachedAll<T>(string path, out T[] cached) where T : Object
        {
            cached = null;

            if (!_resourceListCache.TryGetValue(path, out Dictionary<Type, Object[]> pathResources) ||
                !pathResources.TryGetValue(typeof(T), out Object[] assets))
            {
                return false;
            }

            cached = assets as T[];
            return cached != null;
        }

        private bool TryGetCached(string path, Type type, out Object cached)
        {
            cached = null;

            if (!_resources.TryGetValue(path, out Dictionary<Type, Object> pathResources) ||
                !pathResources.TryGetValue(type, out cached))
            {
                return false;
            }

            return cached != null;
        }

        private void Cache<T>(string path, T asset) where T : Object
            => Cache(path, typeof(T), asset);

        private void Cache(string path, Type type, Object asset)
        {
            if (!_resources.TryGetValue(path, out Dictionary<Type, Object> pathResources))
            {
                pathResources = new Dictionary<Type, Object>();
                _resources.Add(path, pathResources);
            }

            if (pathResources.TryGetValue(type, out Object cached) &&
                cached != null)
            {
                Debug.LogWarning($"Resource already cached. path: {path}, type: {type}");
            }

            pathResources[type] = asset;
        }

        private void CacheAll<T>(string path, T[] assets) where T : Object
            => CacheAll(path, typeof(T), assets);

        private void CacheAll(string path, Type type, Object[] assets)
        {
            if (!_resourceListCache.TryGetValue(path, out Dictionary<Type, Object[]> pathResources))
            {
                pathResources = new Dictionary<Type, Object[]>();
                _resourceListCache.Add(path, pathResources);
            }

            if (pathResources.TryGetValue(type, out Object[] cached) &&
                cached != null)
            {
                Debug.LogWarning($"Resource list already cached. path: {path}, type: {type}");
            }

            pathResources[type] = assets;
        }

        private string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
