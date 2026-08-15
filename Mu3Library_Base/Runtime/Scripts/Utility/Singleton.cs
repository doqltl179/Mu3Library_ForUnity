using UnityEngine;

namespace Mu3Library.Utility
{
    /// <summary>
    /// Finds the single instance that already lives in the scene and never creates one.
    /// <br/> Use <see cref="GenericSingleton{T}"/> instead for an instance that creates itself and
    /// <br/> survives a scene change.
    /// </summary>
    /// <remarks>
    /// This package drives its own services through <see cref="DI.CoreBase"/> and does not use
    /// this base itself. It is kept for a project that consumes the package and wants a plain
    /// singleton component.
    /// </remarks>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (lockObj)
                    {
                        T[] instances = FindObjectsByType<T>(FindObjectsSortMode.None);
                        if (instances.Length == 0)
                        {
                            Debug.LogWarning($"'{typeof(T).Name}' not exist.");
                        }
                        else if (instances.Length > 1)
                        {
                            Debug.LogWarning($"'{typeof(T).Name}' is exist more than one.");

                            // instance가 두 개 이상이라면 하나만 제외하고 전부 삭제한다.
                            for (int i = 1; i < instances.Length; i++)
                            {
                                Destroy(instances[i].gameObject);
                            }
                            _instance = instances[0];
                        }
                        else
                        {
                            _instance = instances[0];
                        }
                    }
                }

                return _instance;
            }
        }
        private static T _instance;

        private static readonly object lockObj = new object();



        protected virtual void OnDestroy()
        {
            if(this == null || gameObject == null)
            {
                return;
            }

            _instance = null;
        }
    }
}