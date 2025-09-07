using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.Base.PostEffect.CommandBufferEffect {
    public class CameraGrayScaleBuffer : CameraBuffer {
        private readonly string bufferName = "GrayScale";

        private Material grayScaleMaterial;



        #region Interface
        public override void Init(Camera bufferCamera, CameraEvent bufferEvent) {
            if(bufferCamera == null) {
                Debug.LogError("Camera is NULL.");

                return;
            }

            if(grayScaleMaterial == null) {
                Shader grayScaleShader = Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/GrayScale");
                if(grayScaleShader == null) {
                    Debug.LogError($"'GrayScale' Shader not found.");

                    return;
                }

                Material mat = new Material(grayScaleShader);

                grayScaleMaterial = mat;
            }

            CommandBuffer cb = new CommandBuffer() { name = bufferName };

            int tempRT = Shader.PropertyToID("_TempRT");
            // 임시 텍스처 생성
            cb.GetTemporaryRT(tempRT, Screen.width, Screen.height, 0, FilterMode.Bilinear);
            // 'tempRT'에 'BuiltinRenderTextureType.CameraTarget'을 복사
            cb.Blit(BuiltinRenderTextureType.CameraTarget, tempRT);
            // 'BuiltinRenderTextureType.CameraTarget'에 'tempRT'를 'grayScaleMaterial'을 적용해서 복사
            cb.Blit(tempRT, BuiltinRenderTextureType.CameraTarget, grayScaleMaterial);
            // 'tempRT' 해제
            cb.ReleaseTemporaryRT(tempRT);

            bufferCamera.AddCommandBuffer(bufferEvent, cb);
            Debug.Log($"GrayScale Buffer Added. Camera: {bufferCamera.gameObject.name}, Event: {cameraEvent}, Buffer Name: {bufferName}");

            buffer = cb;
            camera = bufferCamera;
            cameraEvent = bufferEvent;
        }

        protected override void DestoryAllObjects() {
            if(grayScaleMaterial != null) {
                MonoBehaviour.DestroyImmediate(grayScaleMaterial);
                grayScaleMaterial = null;
            }
        }
        #endregion

        #region Utility
        public void ChangeStrength(float value) {
            grayScaleMaterial.SetFloat("_Strength", value);
        }
        #endregion
    }
}
