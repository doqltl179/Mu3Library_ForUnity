using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Sample.Template.IS
{
    /// <summary>
    /// 원래라면 파일로 저장해야 하지만, 샘플에서는 간단히 메모리 캐시로 대체합니다.
    /// </summary>
    public static class DataCacheAgent
    {
        private static readonly Dictionary<string, string> _cache = new();



        #region Utility
        public static void Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("Key is null or empty.");
                return;
            }

            _cache.Remove(key);

            Debug.Log($"Data removed.\r\nkey: {key}");
        }

        public static string Load(string key)
        {
            if (_cache.TryGetValue(key, out string data))
            {
                return data;
            }

            return "";
        }

        public static void Save(string key, string data)
        {
            if (string.IsNullOrEmpty(key) ||
                string.IsNullOrEmpty(data))
            {
                Debug.LogWarning("Key or data is null or empty.");
                return;
            }

            _cache[key] = data;

            Debug.Log($"Data saved.\r\nkey: {key}\r\ndata: {data}");
        }

        public static void Clear()
        {
            _cache.Clear();

            Debug.Log("Data all cleared.");
        }
        #endregion
    }
}