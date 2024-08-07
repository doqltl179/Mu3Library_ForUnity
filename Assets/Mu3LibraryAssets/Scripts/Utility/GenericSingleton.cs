using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class GenericSingleton<T> : MonoBehaviour where T : MonoBehaviour {
        //private static Dictionary<string, T> instances = new Dictionary<string, T>();
        //public static T Instance {
        //    get {
        //        lock(lockObject) {
        //            inst = null;
        //            componentName = typeof(T).Name;

        //            if(instances.ContainsKey(componentName)) {
        //                inst = instances[componentName];
        //                if(inst == null || inst.gameObject == null) {
        //                    instances.Remove(componentName);

        //                    inst = null;
        //                }
        //            }

        //            if(inst == null) {
        //                T[] temps = FindObjectsOfType<T>();
        //                T instance = null;
        //                if(temps.Length > 0) {
        //                    if(temps.Length > 1) {
        //                        Debug.LogWarning($"Multiple instances of singleton '{componentName}' found. Using the first instance found and destroying others.");
        //                        for(int i = 1; i < temps.Length; i++) {
        //                            Destroy(temps[i].gameObject);
        //                        }
        //                    }
        //                    instance = temps[0];
        //                }
        //                else {
        //                    GameObject go = new GameObject(componentName);
        //                    instance = go.AddComponent<T>();
        //                }

        //                DontDestroyOnLoad(instance.gameObject);
        //                instances.Add(componentName, instance);
        //                inst = instance;
        //            }

        //            return inst;
        //        }
        //    }
        //}

        //private static T inst = null;
        //private static string componentName = "";

        //private static readonly object lockObject = new object();

        public static T Instance {
            get {
                if(instance == null) {
                    lock(lockObj) {
                        T[] instances = FindObjectsOfType<T>();
                        if(instances.Length == 0) {
                            GameObject go = new GameObject(typeof(T).Name);
                            instance = go.AddComponent<T>();
                        }
                        else if(instances.Length == 1) {
                            instance = instances[0];
                        }
                        else if(instances.Length > 1) {
                            Debug.LogWarning($"'{typeof(T).Name}' already exist more than one.");

                            for(int i = 1; i < instances.Length; i++) {
                                Destroy(instances[i].gameObject);
                            }
                            instance = instances[0];
                        }

                        DontDestroyOnLoad(instance.gameObject);
                    }
                }

                return instance;
            }
        }
        private static T instance;

        private static readonly object lockObj = new object();



        private void OnDestroy() {
            Debug.LogWarning($"GenericSingleton Destroyed. type: {typeof(T).Name}");

            instance = null;
        }
    }
}