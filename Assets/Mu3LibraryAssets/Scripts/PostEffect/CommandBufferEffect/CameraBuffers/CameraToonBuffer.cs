using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public class CameraToonBuffer : CameraBuffer {
        private readonly string bufferName = "Toon";

        private Material toonMaterial;



        #region Interface
        public override void Init(Camera bufferCamera, CameraEvent bufferEvent) {
            if(bufferCamera == null) {
                Debug.LogError("Camera is NULL.");

                return;
            }

            if(toonMaterial == null) {
                Shader toonShader = Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/Toon");
                if(toonShader == null) {
                    Debug.LogError($"'Toon' Shader not found.");

                    return;
                }

                Material mat = new Material(toonShader);

                toonMaterial = mat;
            }

            CommandBuffer cb = new CommandBuffer() { name = bufferName };

            int tempRT = Shader.PropertyToID("_TempRT");
            // 임시 텍스처 생성
            //cb.GetTemporaryRT(tempRT, Screen.width, Screen.height, 0, FilterMode.Bilinear);
            cb.GetTemporaryRT(tempRT, 4096, 4096, 0, FilterMode.Bilinear);
            // 'tempRT'에 'BuiltinRenderTextureType.CurrentActive'을 복사
            cb.Blit(BuiltinRenderTextureType.CurrentActive, tempRT);
            // 'BuiltinRenderTextureType.CurrentActive'에 'tempRT'를 'toonMaterial'을 적용해서 복사
            cb.Blit(tempRT, BuiltinRenderTextureType.CurrentActive, toonMaterial);
            // 'tempRT' 해제
            cb.ReleaseTemporaryRT(tempRT);

            bufferCamera.AddCommandBuffer(bufferEvent, cb);
            Debug.Log($"Toon Buffer Added. Camera: {bufferCamera.gameObject.name}, Event: {bufferEvent}, Buffer Name: {bufferName}");

            buffer = cb;
            camera = bufferCamera;
            cameraEvent = bufferEvent;
        }

        protected override void DestoryAllObjects() {
            if(toonMaterial != null) {
                MonoBehaviour.DestroyImmediate(toonMaterial);
                toonMaterial = null;
            }
        }
        #endregion

        #region Utility

        #endregion
    }
}
