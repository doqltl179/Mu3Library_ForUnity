using UnityEngine;

namespace Mu3Library.Extensions
{
    public static class Vector3Extensions
    {
        public static float InverseLerp(this Vector3 value, Vector3 from, Vector3 to)
        {
            Vector3 diffTo = to - from;
            Vector3 diffValue = value - from;

            float sqr = diffTo.sqrMagnitude;
            if (sqr == 0f)
            {
                return 0f;
            }

            float dotVT = Vector3.Dot(diffValue, diffTo);
            return Mathf.Clamp01(dotVT / sqr);
        }
    }
}