using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Extensions
{
    public static class CanvasExtensions
    {
        public static void CopyTo(this Canvas source, Canvas target, bool overwriteScaler = false, bool overwriteRaycaster = false)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            target.renderMode = source.renderMode;
            target.overrideSorting = source.overrideSorting;
            target.pixelPerfect = source.pixelPerfect;
            target.worldCamera = source.worldCamera;
            target.planeDistance = source.planeDistance;
            target.sortingLayerName = source.sortingLayerName;
            target.sortingOrder = source.sortingOrder;
            target.targetDisplay = source.targetDisplay;
            target.additionalShaderChannels = source.additionalShaderChannels;

            if (overwriteScaler &&
                source.TryGetComponent(out GraphicRaycaster raycasterSource) &&
                target.TryGetComponent(out GraphicRaycaster raycasterTarget))
            {
                raycasterSource.CopyTo(raycasterTarget);
            }

            if (overwriteRaycaster &&
                source.TryGetComponent(out CanvasScaler scalerSource) &&
                target.TryGetComponent(out CanvasScaler scalerTarget))
            {
                scalerSource.CopyTo(scalerTarget);
            }
        }
    }
}
