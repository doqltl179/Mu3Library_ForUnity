using Mu3Library.UI.MVP;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.UI.MVP
{
    public static class MVPManagerExtensions
    {
        public static void SetCameraStackAsFirst(this IMVPManager mvpManager, Camera target)
            => SetCameraStack(mvpManager, target, 0);

        public static void SetCameraStack(this IMVPManager mvpManager, Camera target)
            => SetCameraStack(mvpManager, target, -1);

        public static void SetCameraStack(this IMVPManager mvpManager, Camera target, int stackIndex)
        {
            var targetData = target?.GetUniversalAdditionalCameraData();
            if (targetData == null)
            {
                Debug.LogError("Target camera data not found.");
                return;
            }

            var renderCamera = mvpManager?.RenderCamera;
            if (renderCamera == null)
            {
                Debug.LogError("MVP Render Camera not found.");
                return;
            }

            var mvpData = renderCamera.GetUniversalAdditionalCameraData();
            if (mvpData == null)
            {
                Debug.LogError("MVP Render Camera data not found.");
                return;
            }

            CameraRenderType prevTargetRenderType = targetData.renderType;
            targetData.renderType = CameraRenderType.Base;
            if (prevTargetRenderType != targetData.renderType)
            {
                Debug.Log($"Target render type changed. changedTo: {targetData.renderType}");
            }

            targetData.cameraStack.Remove(renderCamera);

            mvpData.renderType = CameraRenderType.Overlay;
            if (stackIndex < 0)
            {
                targetData.cameraStack.Add(renderCamera);
            }
            else if (stackIndex < targetData.cameraStack.Count)
            {
                targetData.cameraStack.Insert(stackIndex, renderCamera);
            }
            else
            {
                Debug.LogWarning($"Stack index is out of range. stackCount: {targetData.cameraStack.Count}, index: {stackIndex}");

                targetData.cameraStack.Add(renderCamera);
            }
        }
    }
}
