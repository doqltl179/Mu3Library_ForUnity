using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public class CameraBlurBuffer : CameraBuffer {
        private readonly string bufferName = "Blur";

        private Material blurMaterial;



        #region Interface
        public override void Init(Camera bufferCamera, CameraEvent bufferEvent) {
            if(bufferCamera == null) {
                Debug.LogError("Camera is NULL.");

                return;
            }

            if(blurMaterial == null) {
                Shader blurShader = Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/Blur");
                if(blurShader == null) {
                    Debug.LogError($"'Blur' Shader not found.");

                    return;
                }

                Material mat = new Material(blurShader);

                blurMaterial = mat;
            }

            CommandBuffer cb = new CommandBuffer() { name = bufferName };

            int tempRT = Shader.PropertyToID("_TempRT");
            // 임시 텍스처 생성
            cb.GetTemporaryRT(tempRT, Screen.width, Screen.height, 0, FilterMode.Bilinear);
            // 'tempRT'에 'BuiltinRenderTextureType.CameraTarget'을 복사
            cb.Blit(BuiltinRenderTextureType.CameraTarget, tempRT);
            // 'BuiltinRenderTextureType.CameraTarget'에 'tempRT'를 'blurMaterial'을 적용해서 복사
            cb.Blit(tempRT, BuiltinRenderTextureType.CameraTarget, blurMaterial);
            // 'tempRT' 해제
            cb.ReleaseTemporaryRT(tempRT);

            bufferCamera.AddCommandBuffer(bufferEvent, cb);
            Debug.Log($"Blur Buffer Added. Camera: {bufferCamera.gameObject.name}, Event: {bufferEvent}, Buffer Name: {bufferName}");

            buffer = cb;
            camera = bufferCamera;
            cameraEvent = bufferEvent;
        }

        protected override void DestoryAllObjects() {
            if(blurMaterial != null) {
                MonoBehaviour.DestroyImmediate(blurMaterial);
                blurMaterial = null;
            }
        }
        #endregion

        #region Utility
        public void ChangeStrength(float value) {
            blurMaterial.SetFloat("_Strength", value);
        }

        public void ChangeAmount(float value) {
            blurMaterial.SetFloat("_BlurAmount", value);
        }

        public void ChangeKernelSize(int value) {
            blurMaterial.SetInt("_KernelSize", value);
        }
        #endregion
    }
}
