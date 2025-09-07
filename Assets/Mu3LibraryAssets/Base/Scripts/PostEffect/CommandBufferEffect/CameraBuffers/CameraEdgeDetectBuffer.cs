using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public class CameraEdgeDetectBuffer : CameraBuffer {
        private readonly string bufferName = "EdgeDetect";

        private Material edgeDetectMaterial;



        #region Interface
        public override void Init(Camera bufferCamera, CameraEvent bufferEvent) {
            if(bufferCamera == null) {
                Debug.LogError("Camera is NULL.");

                return;
            }

            if(edgeDetectMaterial == null) {
                Shader edgeDetectShader = Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/EdgeDetect");
                if(edgeDetectShader == null) {
                    Debug.LogError($"'EdgeDetect' Shader not found.");

                    return;
                }

                Material mat = new Material(edgeDetectShader);

                edgeDetectMaterial = mat;
            }

            CommandBuffer cb = new CommandBuffer() { name = bufferName };

            int tempRT = Shader.PropertyToID("_TempRT");
            // 임시 텍스처 생성
            cb.GetTemporaryRT(tempRT, Screen.width, Screen.height, 0, FilterMode.Bilinear);
            // 'tempRT'에 'BuiltinRenderTextureType.CameraTarget'을 복사
            cb.Blit(BuiltinRenderTextureType.CameraTarget, tempRT);
            // 'BuiltinRenderTextureType.CameraTarget'에 'tempRT'를 'edgeDetectMaterial'을 적용해서 복사
            cb.Blit(tempRT, BuiltinRenderTextureType.CameraTarget, edgeDetectMaterial);
            // 'tempRT' 해제
            cb.ReleaseTemporaryRT(tempRT);

            bufferCamera.AddCommandBuffer(bufferEvent, cb);
            Debug.Log($"edgeDetect Buffer Added. Camera: {bufferCamera.gameObject.name}, Event: {bufferEvent}, Buffer Name: {bufferName}");

            buffer = cb;
            camera = bufferCamera;
            cameraEvent = bufferEvent;
        }

        protected override void DestoryAllObjects() {
            if(edgeDetectMaterial != null) {
                MonoBehaviour.DestroyImmediate(edgeDetectMaterial);
                edgeDetectMaterial = null;
            }
        }
        #endregion

        #region Utility
        public void ChangeColor(Color value) {
            edgeDetectMaterial.SetColor("_EdgeColor", value);
        }

        public void ChangeFactor(float value) {
            edgeDetectMaterial.SetFloat("_EdgeFactor", value);
        }

        public void ChangeThickness(float value) {
            edgeDetectMaterial.SetFloat("_EdgeThickness", value);
        }
        #endregion
    }
}
