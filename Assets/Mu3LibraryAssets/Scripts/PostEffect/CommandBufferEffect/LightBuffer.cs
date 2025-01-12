using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public class LightBuffer {
        private Light light = null;
        private CommandBuffer buffer = null;

        public LightEvent LightEvent {
            get => lightEvent;
        }
        private LightEvent lightEvent;

        public Shader Shader {
            get => shader;
        }
        private Shader shader;

        public Material Mat {
            get => mat;
        }
        private Material mat = null;

        public bool IsBufferExist {
            get => buffer != null;
        }

        public string BufferName {
            get => buffer != null ? buffer.name : "";
        }

        private int tempRT = -1;

        private Dictionary<string, int> tempIntProperties = new Dictionary<string, int>();
        private Dictionary<string, float> tempFloatProperties = new Dictionary<string, float>();
        private Dictionary<string, Color> tempColorProperties = new Dictionary<string, Color>();



        public LightBuffer(string bufferName, Light bufferLight, LightEvent bufferEvent, Shader bufferShader) {
            if(bufferLight == null) {
                Debug.LogError("Light is NULL.");

                return;
            }
            if(bufferShader == null) {
                Debug.LogError("Shader is NULL.");

                return;
            }

            tempRT = Shader.PropertyToID("_TempRT");
            Material bufferMaterial = new Material(bufferShader);

            CommandBuffer cb = new CommandBuffer() { name = bufferName };
            cb.GetTemporaryRT(tempRT, Screen.width, Screen.height, 0, FilterMode.Bilinear);
            cb.Blit(BuiltinRenderTextureType.CameraTarget, tempRT);
            cb.Blit(tempRT, BuiltinRenderTextureType.CameraTarget, bufferMaterial);
            cb.ReleaseTemporaryRT(tempRT);

            bufferLight.AddCommandBuffer(bufferEvent, cb);
            Debug.Log($"Command Buffer Added. Light: {bufferLight.gameObject.name}, Event: {bufferEvent}, Buffer Name: {bufferName}");

            buffer = cb;
            light = bufferLight;
            lightEvent = bufferEvent;
            shader = bufferShader;
            mat = bufferMaterial;


        }

        #region Utility
        int tempInt;
        public void ChangeProperty(string propertyName, int value) {
            if(mat == null) {
                Debug.LogError($"Material is NULL.");

                return;
            }

            if(tempIntProperties.TryGetValue(propertyName, out tempInt)) {
                if(tempInt != value) {
                    tempIntProperties[propertyName] = value;
                    mat.SetInt(propertyName, value);
                }
            }
            else {
                tempIntProperties.Add(propertyName, value);
                mat.SetInt(propertyName, value);
            }
        }

        float tempFloat;
        public void ChangeProperty(string propertyName, float value) {
            if(mat == null) {
                Debug.LogError($"Material is NULL.");

                return;
            }

            if(tempFloatProperties.TryGetValue(propertyName, out tempFloat)) {
                if(tempFloat != value) {
                    tempFloatProperties[propertyName] = value;
                    mat.SetFloat(propertyName, value);
                }
            }
            else {
                tempFloatProperties.Add(propertyName, value);
                mat.SetFloat(propertyName, value);
            }
        }

        Color tempColor;
        public void ChangeProperty(string propertyName, Color value) {
            if(mat == null) {
                Debug.LogError($"Material is NULL.");

                return;
            }

            if(tempColorProperties.TryGetValue(propertyName, out tempColor)) {
                if(tempColor != value) {
                    tempColorProperties[propertyName] = value;
                    mat.SetColor(propertyName, value);
                }
            }
            else {
                tempColorProperties.Add(propertyName, value);
                mat.SetColor(propertyName, value);
            }
        }

        public void Clear() {
            if(light != null) {
                if(buffer != null) {
                    light.RemoveCommandBuffer(lightEvent, buffer);
                    Debug.Log($"Command Buffer Removed. Light: {light.gameObject.name}, Event: {lightEvent}, Buffer Name: {buffer.name}");
                }
                light = null;
            }

            if(buffer != null) {
                buffer.Release();
                buffer = null;
            }

            if(mat != null) {
                MonoBehaviour.DestroyImmediate(mat);
                mat = null;
            }

            tempIntProperties.Clear();
            tempFloatProperties.Clear();
            tempColorProperties.Clear();
        }
        #endregion
    }
}
