using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CameraUtil {
    public abstract class CameraMoveSystem {
        public PlayState State => state;
        protected PlayState state;

        public bool IsPlaying => isPlaying;
        private bool isPlaying;



        public abstract void Move(Camera cam);

        public abstract void SetProperties(object[] param = null);

        public virtual void Play() {
            isPlaying = true;
        }

        public virtual void Stop() {
            isPlaying = false;
        }
    }
}