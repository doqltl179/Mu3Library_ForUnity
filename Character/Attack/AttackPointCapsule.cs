using Mu3Library.Raycast;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public class AttackPointCapsule : AttackPoint {



#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if(enabled) {
                Gizmos.color = Color.red;

                Editor.Gizmo.Draw.WireCapsule(transform, radius, height);
            }
        }
#endif

        public override void Init(AttackInfo info, int layerMask = -1) {
            attackInfo = info;

            rayHelper = new RayCapsuleHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                radius,
                height,
                0.0f,
                layerMask);
        }
    }
}