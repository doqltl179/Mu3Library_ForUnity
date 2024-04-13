using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Weapon {
    public interface IThrow {
        public abstract void InitThrow(float standardTime, float standardDistance, float angleOffset, int targetLayerMask = -1);
        public abstract void Throw(Vector3 from, Vector3 to);
    }
}