using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
        private static Dictionary<string, T> instances = new Dictionary<string, T>();
        public static T Instance {
            get {
                T inst = null;
                string componentName = typeof(T).Name;
                if(instances.TryGetValue(componentName, out inst)) {
                    if(inst == null) {
                        instances.Remove(componentName);
                    }
                }

                if(inst == null) {
                    T[] temps = FindObjectsOfType<T>();
                    if(temps.Length > 0) {
                        for(int i = 1; i < temps.Length; i++) {
                            Destroy(temps[i].gameObject);
                        }
                        inst = temps[0];
                    }

                    instances.Add(componentName, inst);
                }

                return inst;
            }
        }
    }
}