using UnityEngine;

namespace Mu3Library.Utility
{
    public class GenericSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObj)
                    {
                        T[] instances = FindObjectsByType<T>(FindObjectsSortMode.None);
                        if (instances.Length == 0)
                        {
                            GameObject go = new GameObject(typeof(T).Name);
                            instance = go.AddComponent<T>();
                        }
                        else if (instances.Length == 1)
                        {
                            instance = instances[0];
                        }
                        else if (instances.Length > 1)
                        {
                            Debug.LogWarning($"'{typeof(T).Name}' already exist more than one.");

                            // instance가 두 개 이상이라면 하나만 제외하고 전부 삭제한다.
                            for (int i = 1; i < instances.Length; i++)
                            {
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



        protected virtual void OnDestroy()
        {
            instance = null;
        }
    }
}