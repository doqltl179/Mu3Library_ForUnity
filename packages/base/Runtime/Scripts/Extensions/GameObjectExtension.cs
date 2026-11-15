using UnityEngine;

namespace Mu3Library.Extensions
{
    public static class GameObjectExtensions
    {
        public static void SetLayerWithChildren(this GameObject go, int layer)
        {
            go.layer = layer;

            Transform transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetLayerWithChildren(layer);
            }
        }

        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (!go.TryGetComponent(out T result))
            {
                result = go.AddComponent<T>();
            }

            return result;
        }
    }
}
