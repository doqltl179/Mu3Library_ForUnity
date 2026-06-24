using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.Cam
{
    public static class CameraStackSetter
    {
        public static void SetCameraStackToMainAsFirst(Camera stackCam)
            => SetCameraStack(Camera.main, stackCam, 0);

        public static void SetCameraStackToMain(Camera stackCam)
            => SetCameraStack(Camera.main, stackCam, -1);

        public static void SetCameraStackToMain(Camera stackCam, int stackIndex)
            => SetCameraStack(Camera.main, stackCam, stackIndex);

        public static void SetCameraStackAsFirst(Camera rootCam, Camera stackCam)
            => SetCameraStack(rootCam, stackCam, 0);

        public static void SetCameraStack(Camera rootCam, Camera stackCam)
            => SetCameraStack(rootCam, stackCam, -1);

        public static void SetCameraStack(Camera rootCam, Camera stackCam, int stackIndex)
        {
            var stackCamData = stackCam?.GetUniversalAdditionalCameraData();
            if (stackCamData == null)
            {
                Debug.LogError("Stack camera data not found.");
                return;
            }

            var rootCamData = rootCam?.GetUniversalAdditionalCameraData();
            if (rootCamData == null)
            {
                Debug.LogError("Root camera data not found.");
                return;
            }

            CameraRenderType prevRootCamRenderType = rootCamData.renderType;
            rootCamData.renderType = CameraRenderType.Base;
            if (prevRootCamRenderType != rootCamData.renderType)
            {
                Debug.LogWarning($"Root camera render type changed. changedTo: {rootCamData.renderType}");
            }

            rootCamData.cameraStack.Remove(stackCam);

            stackCamData.renderType = CameraRenderType.Overlay;
            if (stackIndex < 0)
            {
                rootCamData.cameraStack.Add(stackCam);
            }
            else if (stackIndex < rootCamData.cameraStack.Count)
            {
                rootCamData.cameraStack.Insert(stackIndex, stackCam);
            }
            else
            {
                Debug.LogWarning($"Stack index is out of range. stackCount: {rootCamData.cameraStack.Count}, index: {stackIndex}");

                rootCamData.cameraStack.Add(stackCam);
            }
        }
    }
}
