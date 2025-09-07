using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public abstract class LightBuffer : ILightBuffer {
        protected Light light = null;
        protected CommandBuffer buffer = null;

        public LightEvent LightEvent {
            get => lightEvent;
        }
        protected LightEvent lightEvent;

        public bool IsBufferExist {
            get => buffer != null;
        }

        public string BufferName {
            get => buffer != null ? buffer.name : "";
        }



        #region Interface
        public abstract void Init(Light bufferCamera, LightEvent bufferEvent);

        public void Clear() {
            if(light != null) {
                if(buffer != null) {
                    light.RemoveCommandBuffer(lightEvent, buffer);
                    Debug.Log($"Light Command Buffer Removed. Light: {light.gameObject.name}, Event: {lightEvent}, Buffer Name: {buffer.name}");
                }
                light = null;
            }

            if(buffer != null) {
                buffer.Release();
                buffer = null;
            }

            DestoryAllObjects();
        }

        protected abstract void DestoryAllObjects();
        #endregion
    }
}
