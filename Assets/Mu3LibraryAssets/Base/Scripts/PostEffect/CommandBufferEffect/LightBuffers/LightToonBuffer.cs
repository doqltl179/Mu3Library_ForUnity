using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    /// <summary>
    /// 지금 정상 동작 안함
    /// </summary>
    public class LightToonBuffer : LightBuffer {
        private readonly string bufferName = "Toon";

        private Material toonMaterial;



        #region Interface
        public override void Init(Light bufferLight, LightEvent bufferEvent) {
            if(bufferLight == null) {
                Debug.LogError("Light is NULL.");

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
            cb.GetTemporaryRT(tempRT, Screen.width, Screen.height, 0, FilterMode.Bilinear);
            // 'tempRT'에 'BuiltinRenderTextureType'을 복사
            cb.Blit(BuiltinRenderTextureType.CurrentActive, tempRT);
            // 'BuiltinRenderTextureType'에 'tempRT'를 'toonMaterial'을 적용해서 복사
            cb.Blit(tempRT, BuiltinRenderTextureType.CurrentActive, toonMaterial);
            // 'tempRT' 해제
            cb.ReleaseTemporaryRT(tempRT);

            bufferLight.AddCommandBuffer(bufferEvent, cb);
            Debug.Log($"Toon Buffer Added. Light: {bufferLight.gameObject.name}, Event: {bufferEvent}, Buffer Name: {bufferName}");

            buffer = cb;
            light = bufferLight;
            lightEvent = bufferEvent;
        }

        protected override void DestoryAllObjects() {
            if(toonMaterial != null) {
                MonoBehaviour.DestroyImmediate(toonMaterial);
                toonMaterial = null;
            }
        }
        #endregion

        #region Utility
        public void ChangeShadingStep(float value) {
            toonMaterial.SetFloat("_ShadingStep", value);
        }
        #endregion
    }
}
