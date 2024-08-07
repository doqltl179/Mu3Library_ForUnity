using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
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
        //                T instance = null;
        //                T[] temps = FindObjectsOfType<T>();
        //                if(temps.Length > 0) {
        //                    for(int i = 1; i < temps.Length; i++) {
        //                        Destroy(temps[i].gameObject);
        //                    }
        //                    instance = temps[0];
        //                }

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
                            Debug.LogWarning($"'{typeof(T).Name}' not exist.");
                        }
                        else if(instances.Length > 1) {
                            Debug.LogWarning($"'{typeof(T).Name}' is exist more than one.");

                            for(int i = 1; i < instances.Length; i++) {
                                Destroy(instances[i].gameObject);
                            }
                            instance = instances[0];
                        }
                    }
                }

                return instance;
            }
        }
        private static T instance;

        private static readonly object lockObj = new object();



        private void OnDestroy() {
            instance = null;
        }
    }
}