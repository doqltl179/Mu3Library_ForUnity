using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Raycast {
    public class FloorContactRayHelper {
        public Transform Origin { get; private set; }
        public float HeightOffset { get; private set; }
        public float HeightSensitive { get; private set; }
        public int LayerMask { get; private set; }

        private RaycastHit Hit;
        public float FloorDistance { get; private set; }
        public bool OnFloor { get; private set; }



        public FloorContactRayHelper(Transform origin, float heightOffset, float heightSensitive, int layerMask = -1) {
            Origin = origin;
            HeightOffset = heightOffset;
            HeightSensitive = heightSensitive;
            LayerMask = layerMask;
        }

        #region Raycast
        public bool Raycast() {
            if(Physics.Raycast(
                Origin.position + Vector3.up * HeightOffset,
                Vector3.down,
                out Hit,
                float.MaxValue,
                LayerMask)) {
                FloorDistance = (Origin.position.y - Hit.point.y) - HeightOffset;
                OnFloor = FloorDistance < HeightSensitive;

                return true;
            }
            else {
                FloorDistance = float.MaxValue;
                OnFloor = false;

                return false;
            }
        }
        #endregion
    }
}