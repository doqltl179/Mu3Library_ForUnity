using UnityEngine;

namespace Mu3Library.Extension
{
    public static class TransformExtension
    {
        public static void LocalToOrigin(this Transform transform)
        {
            LocalPositionToZero(transform);
            LocalEulerToZero(transform);
            ScaleToOne(transform);
        }

        public static void LocalPositionToZero(this Transform transform) => transform.localPosition = Vector3.zero;

        public static void LocalEulerToZero(this Transform transform) => transform.localEulerAngles = Vector3.zero;
        
        public static void ScaleToOne(this Transform transform) => transform.localScale = Vector3.one;
    }
}
