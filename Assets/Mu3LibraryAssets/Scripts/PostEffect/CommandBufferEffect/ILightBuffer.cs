using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.PostEffect.CommandBufferEffect {
    public interface ILightBuffer {
        public void Init(Light bufferCamera, LightEvent bufferEvent);
        public void Clear();
    }
}
