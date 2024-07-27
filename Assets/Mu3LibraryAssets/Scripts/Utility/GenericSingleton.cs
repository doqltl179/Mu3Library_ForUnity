using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class GenericSingleton<T> : MonoBehaviour where T : MonoBehaviour {
        private static Dictionary<string, T> instances = new Dictionary<string, T>();
        public static T Instance {
            get {
                lock(lockObject) {
                    inst = null;
                    componentName = typeof(T).Name;

                    if(instances.ContainsKey(componentName)) {
                        inst = instances[componentName];
                        if(inst == null || inst.gameObject == null) {
                            instances.Remove(componentName);

                            inst = null;
                        }
                    }
                    
                    if(inst == null) {
                        T[] temps = FindObjectsOfType<T>();
                        T instance = null;
                        if(temps.Length > 0) {
                            if(temps.Length > 1) {
                                Debug.LogWarning($"Multiple instances of singleton '{componentName}' found. Using the first instance found and destroying others.");
                                for(int i = 1; i < temps.Length; i++) {
                                    Destroy(temps[i].gameObject);
                                }
                            }
                            instance = temps[0];
                        }
                        else {
                            GameObject go = new GameObject(componentName);
                            instance = go.AddComponent<T>();
                        }

                        DontDestroyOnLoad(instance.gameObject);
                        instances.Add(componentName, instance);
                        inst = instance;
                    }

                    return inst;
                }
            }
        }

        private static T inst = null;
        private static string componentName = "";

        private static readonly object lockObject = new object();
    }
}