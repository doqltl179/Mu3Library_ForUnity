using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public abstract class CameraBuffer : ICameraBuffer {
        protected Camera camera = null;
        protected CommandBuffer buffer = null;

        public CameraEvent CameraEvent {
            get => cameraEvent;
        }
        protected CameraEvent cameraEvent;

        public bool IsBufferExist {
            get => buffer != null;
        }

        public string BufferName {
            get => buffer != null ? buffer.name : "";
        }



        #region Interface
        public abstract void Init(Camera bufferCamera, CameraEvent bufferEvent);

        public void Clear() {
            if(camera != null) {
                if(buffer != null) {
                    camera.RemoveCommandBuffer(cameraEvent, buffer);
                    Debug.Log($"Camera Command Buffer Removed. Camera: {camera.gameObject.name}, Event: {cameraEvent}, Buffer Name: {buffer.name}");
                }
                camera = null;
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
