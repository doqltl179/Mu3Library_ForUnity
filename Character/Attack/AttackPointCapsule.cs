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

                if(rayHelper == null) Editor.Gizmo.Draw.WireCapsule(transform, m_radius * rayScale, m_height * rayScale);
                else Editor.Gizmo.Draw.WireCapsule(rayHelper.Origin, rayHelper.Radius * rayScale, rayHelper.Height * rayScale);
            }
        }
#endif

        public override void Init(AttackInfo info, int layerMask = -1) {
            attackInfo = info;

            rayHelper = new RayCapsuleHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                m_radius,
                m_height,
                0.0f,
                layerMask);
        }
    }
}