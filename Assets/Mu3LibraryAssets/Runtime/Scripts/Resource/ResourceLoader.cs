using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Resource
{
    public partial class ResourceLoader : IResourceLoader, IDisposable
    {
        private readonly Dictionary<string, Dictionary<Type, Object>> _resources = new();



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

            Object[] loaded = Resources.LoadAll<T>(path);
            if (loaded == null || loaded.Length == 0)
            {
                return Array.Empty<T>();
            }

            T[] results = new T[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                Object asset = loaded[i];
                T result = asset as T;

                if (result != null)
                {
                    results[i] = result;

                    Cache(path, result);
                }
            }

            return results;
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

            if (!_resources.TryGetValue(path, out var pathResources) ||
                !pathResources.TryGetValue(type, out Object asset))
            {
                return false;
            }

            pathResources.Remove(type);
            if (asset != null)
            {
                Resources.UnloadAsset(asset);
            }

            if (pathResources.Count == 0)
            {
                _resources.Remove(path);
            }

            return true;
        }

        public void ReleaseAll(string path)
        {
            if (!_resources.TryGetValue(path, out var pathResources))
            {
                Debug.LogWarning($"No cached resources found for path: {path}");
                return;
            }

            foreach (var entry in pathResources)
            {
                Object asset = entry.Value;
                if (asset != null)
                {
                    Resources.UnloadAsset(asset);
                }
            }

            _resources.Remove(path);
        }

        public void ReleaseAll()
        {
            HashSet<Object> uniqueAssets = new HashSet<Object>();
            foreach (var entry in _resources.Values)
            {
                foreach (Object asset in entry.Values)
                {
                    if (asset != null)
                    {
                        uniqueAssets.Add(asset);
                    }
                }
            }

            foreach (Object asset in uniqueAssets)
            {
                Resources.UnloadAsset(asset);
            }

            _resources.Clear();
        }

        private bool TryGetCached<T>(string path, out Object cached) where T : Object
            => TryGetCached(path, typeof(T), out cached);

        private bool TryGetCached(string path, Type type, out Object cached)
        {
            cached = null;

            if (!_resources.TryGetValue(path, out var pathResources) ||
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
            if (!_resources.TryGetValue(path, out var pathResources))
            {
                pathResources = new Dictionary<Type, Object>();
                _resources.Add(path, pathResources);
            }

            if (!pathResources.TryGetValue(type, out Object cached) ||
                cached == null)
            {
                pathResources[type] = asset;
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
