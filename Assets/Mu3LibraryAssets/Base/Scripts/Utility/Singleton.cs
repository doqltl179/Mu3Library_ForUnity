using UnityEngine;

namespace Mu3Library.Base.Utility {
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
        public static T Instance {
            get {
                if(instance == null) {
                    lock(lockObj) {
                        T[] instances = FindObjectsByType<T>(FindObjectsSortMode.None);
                        if(instances.Length == 0) {
                            Debug.LogWarning($"'{typeof(T).Name}' not exist.");
                        }
                        else if(instances.Length > 1) {
                            Debug.LogWarning($"'{typeof(T).Name}' is exist more than one.");

                            // instance가 두 개 이상이라면 하나만 제외하고 전부 삭제한다.
                            for(int i = 1; i < instances.Length; i++) {
                                Destroy(instances[i].gameObject);
                            }
                            instance = instances[0];
                        }
                        else {
                            instance = instances[0];
                        }
                    }
                }

                return instance;
            }
        }
        private static T instance;

        private static readonly object lockObj = new object();



        protected virtual void OnDestroy() {
            instance = null;
        }
    }
}