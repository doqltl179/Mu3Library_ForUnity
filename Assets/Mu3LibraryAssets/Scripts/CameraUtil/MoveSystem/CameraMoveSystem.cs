using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CameraUtil {
    public abstract class CameraMoveSystem {
        public PlayState State => state;
        protected PlayState state;

        public abstract void Move(Camera cam);
    }
}