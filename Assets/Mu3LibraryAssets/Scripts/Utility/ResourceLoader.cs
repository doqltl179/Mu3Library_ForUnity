using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library
{
    /// <summary>
    /// <br/> Resource를 캐싱하여 관리하기 위해 만듬
    /// <br/> Resource를 반환하는 것으로 instantiate가 된 게임 오브젝트를 반환하는 것이 아님.
    /// </summary>
    public static class ResourceLoader
    {
        private static Dictionary<string, Dictionary<Type, Object>> _resources = new Dictionary<string, Dictionary<Type, Object>>();



        /// <summary>
        /// path: 파일 경로
        /// </summary>
        public static T GetResource<T>(string path) where T : Object
        {
            path = ReplacePath(path);

            T resource = GetLoadedObject<T>(path);
            if (resource == null)
            {
                resource = Resources.Load<T>(path);
                if (resource == null)
                {
                    Debug.LogError($"Object not found in resources folder. path: {path}");
                    return null;
                }

                Dictionary<Type, Object> pathResources = null;
                if (!_resources.TryGetValue(path, out pathResources))
                {
                    pathResources = new Dictionary<Type, Object>();
                    _resources.Add(path, pathResources);
                }

                Type type = typeof(T);
                pathResources.Add(type, resource);
            }

            return resource;
        }

        /// <summary>
        /// <br/> path: 폴더 경로
        /// <br/> 하위 폴더는 탐색에 포함되지 않는다.
        /// </summary>
        public static T[] GetAllResources<T>(string path) where T : Object
        {
            path = ReplacePath(path);
            Type type = typeof(T);

            T[] result = Resources.LoadAll<T>(path);
            foreach (T resource in result)
            {
                string filePath = $"path/{resource.name}";
                if (GetLoadedObject<T>(filePath) == null)
                {
                    Dictionary<Type, Object> pathResources = null;
                    if (!_resources.TryGetValue(filePath, out pathResources))
                    {
                        pathResources = new Dictionary<Type, Object>();
                        _resources.Add(filePath, pathResources);
                    }

                    pathResources.Add(type, resource);
                }
            }

            return result;
        }

        /// <summary>
        /// path: 파일 경로
        /// </summary>
        private static T GetLoadedObject<T>(string path) where T : Object
        {
            if (!_resources.ContainsKey(path))
            {
                return null;
            }

            Type type = typeof(T);
            if (!_resources[path].ContainsKey(type))
            {
                return null;
            }

            return (T)_resources[path][type];
        }

        private static string ReplacePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}