using UnityEngine;

namespace Mu3Library.Extensions
{
    public static class RectTransformExtensions
    {
        /// <summary>
        /// Anchors the rect to a normalized range of its parent.
        /// While <paramref name="fill"/> is on the offsets are cleared, so the rect covers the range exactly.
        /// </summary>
        public static void AnchorTo(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, bool fill = true)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;

            if (!fill)
            {
                return;
            }

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Stretch(this RectTransform rt) => rt.AnchorTo(Vector2.zero, Vector2.one);

        public static void AnchorPositionToZero(this RectTransform rt) => rt.anchoredPosition = Vector2.zero;
        public static void AnchorPosition3DToZero(this RectTransform rt) => rt.anchoredPosition3D = Vector3.zero;
    }
}
