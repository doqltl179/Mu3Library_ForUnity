using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public static class ResourceLoader {
        private static Dictionary<string, UnityEngine.Object> resources = new Dictionary<string, UnityEngine.Object>();



        /// <summary>
        /// Resource를 반환하는 것으로 instantiate가 된 게임 오브젝트를 반환하는 것이 아님.
        /// </summary>
        public static T GetResource<T>(string path) where T : UnityEngine.Object {
            UnityEngine.Object resource = null;
            if(resources.TryGetValue(path, out resource)) {
                return (T)resource;
            }
            else {
                resource = Resources.Load<T>(path);
                if(resource == null) {
                    Debug.LogError(string.Format("Object not found in resources folder. path: ", path));

                    return null;
                }

                resources.Add(path, resource);
                return (T)resource;
            }
        }
    }
}