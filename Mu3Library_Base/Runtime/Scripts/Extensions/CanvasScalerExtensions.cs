using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Extensions
{
    public static class CanvasScalerExtensions
    {
        public static void CopyTo(this CanvasScaler source, CanvasScaler target)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            target.uiScaleMode = source.uiScaleMode;
            target.referencePixelsPerUnit = source.referencePixelsPerUnit;
            target.scaleFactor = source.scaleFactor;
            target.referenceResolution = source.referenceResolution;
            target.screenMatchMode = source.screenMatchMode;
            target.matchWidthOrHeight = source.matchWidthOrHeight;
            target.physicalUnit = source.physicalUnit;
            target.fallbackScreenDPI = source.fallbackScreenDPI;
            target.defaultSpriteDPI = source.defaultSpriteDPI;
            target.dynamicPixelsPerUnit = source.dynamicPixelsPerUnit;
        }
    }
}
