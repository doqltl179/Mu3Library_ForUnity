using UnityEngine;

namespace Mu3Library.Extension
{
    public static class GameObjectExtension
    {
        public static T GetOrAddComponent<T>(this GameObject go)where T : Component
        {
            T result = go.GetComponent<T>();

            if (result == null)
            {
                result = go.AddComponent<T>();
            }

            return result;
        }
    }
}
