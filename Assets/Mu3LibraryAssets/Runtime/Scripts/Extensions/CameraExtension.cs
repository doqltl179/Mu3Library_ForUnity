using UnityEngine;

namespace Mu3Library.Extensions
{
    public static class CameraExtension
    {
        public static void ApplyPropertiesTo(this Camera source, Camera target)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("source or target is null.");
                return;
            }

            ApplyProperties(source, target);
        }

        public static void ApplyPropertiesFrom(this Camera target, Camera source)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("source or target is null.");
                return;
            }

            ApplyProperties(source, target);
        }

        private static void ApplyProperties(Camera source, Camera target)
        {
            target.clearFlags = source.clearFlags;
            target.backgroundColor = source.backgroundColor;
            target.cullingMask = source.cullingMask;
            target.orthographic = source.orthographic;
            target.orthographicSize = source.orthographicSize;
            target.fieldOfView = source.fieldOfView;
            target.nearClipPlane = source.nearClipPlane;
            target.farClipPlane = source.farClipPlane;
            target.rect = source.rect;
            target.depth = source.depth;
            target.renderingPath = source.renderingPath;
            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;
            target.allowDynamicResolution = source.allowDynamicResolution;
            target.useOcclusionCulling = source.useOcclusionCulling;
            target.targetTexture = source.targetTexture;
            target.targetDisplay = source.targetDisplay;
            target.eventMask = source.eventMask;
            target.layerCullDistances = source.layerCullDistances;
            target.layerCullSpherical = source.layerCullSpherical;
            target.depthTextureMode = source.depthTextureMode;
            target.aspect = source.aspect;
        }
    }
}
