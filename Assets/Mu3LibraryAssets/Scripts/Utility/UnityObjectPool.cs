using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class UnityObjectPoolManager: GenericSingleton<UnityObjectPoolManager>{
        private static Dictionary<string, List<MonoBehaviour>> pool = new Dictionary<string, List<MonoBehaviour>>();



        public void Init() {
            if(pool != null && pool.Keys.Count > 0) {
                foreach(string key in pool.Keys) {
                    foreach(MonoBehaviour obj in pool[key]) {
                        if(obj.gameObject != null) Destroy(obj.gameObject);
                    }
                }
            }
            pool = new Dictionary<string, List<MonoBehaviour>>();
        }

        public void AddObject<T>(T obj) where T : MonoBehaviour {
            obj.transform.SetParent(transform);

            string typeName = typeof(T).Name;
            List<MonoBehaviour> targetList = null;
            if(pool.TryGetValue(typeName, out targetList)) {

            }
            else {
                targetList = new List<MonoBehaviour>();
                pool.Add(typeName, targetList);
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

                    for(int i = 0; i <= objectIndex; i++) {
                        targetList.RemoveAt(i);
                    }
                    pool[typeName] = targetList;
                }
            }

            return obj;
        }
    }
}