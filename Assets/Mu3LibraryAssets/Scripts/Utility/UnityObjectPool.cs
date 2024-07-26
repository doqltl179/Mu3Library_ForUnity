using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class UnityObjectPoolManager: GenericSingleton<UnityObjectPoolManager>{
        private static Dictionary<string, List<MonoBehaviour>> pool = new Dictionary<string, List<MonoBehaviour>>();



        public void Init() {
            if(pool != null && pool.Keys.Count > 0) {
                foreach(List<MonoBehaviour> components in pool.Values) {
                    for(int i = 0; i < components.Count; i++) {
                        if(components[i].gameObject != null) components[i].gameObject.SetActive(true);
                    }
                }
            }
            pool = new Dictionary<string, List<MonoBehaviour>>();
        }

        public void AddObject<T>(T obj) where T : MonoBehaviour {
            string typeName = typeof(T).Name;
            if(pool.ContainsKey(typeName)) { 
                pool.Add(typeName, new List<MonoBehaviour>());
            }

            obj.gameObject.SetActive(false);
            pool[typeName].Add(obj);
        }

        public T GetObject<T>() where T : MonoBehaviour {
            T obj = null;

            string typeName = typeof(T).Name;
            List<MonoBehaviour> targetList = null;
            if(pool.TryGetValue(typeName, out targetList)) {
                int objectIndex = targetList.FindIndex(t => t.gameObject != null);
                if(objectIndex >= 0) {
                    obj = (T)targetList[objectIndex];

                    targetList.RemoveRange(0, objectIndex + 1);
                    pool[typeName] = targetList;
                }
            }

            return obj;
        }
    }
}