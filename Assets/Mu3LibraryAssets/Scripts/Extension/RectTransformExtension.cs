using UnityEngine;

namespace Mu3Library.Extension
{
    public static class RectTransformExtension
    {
        public static void Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void AnchorPositionToZero(this RectTransform rt) => rt.anchoredPosition = Vector2.zero;
        public static void AnchorPosition3DToZero(this RectTransform rt) => rt.anchoredPosition3D = Vector3.zero;
    }
}
