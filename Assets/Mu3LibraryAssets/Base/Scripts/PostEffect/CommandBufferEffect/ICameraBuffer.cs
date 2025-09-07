using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public interface ICameraBuffer {
        public void Init(Camera bufferCamera, CameraEvent bufferEvent);
        public void Clear();
    }
}
