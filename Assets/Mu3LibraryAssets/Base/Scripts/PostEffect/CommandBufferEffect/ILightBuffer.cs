using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.Base.PostEffect.CommandBufferEffect {
    public interface ILightBuffer {
        public void Init(Light bufferLight, LightEvent bufferEvent);
        public void Clear();
    }
}
