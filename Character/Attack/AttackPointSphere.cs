using Mu3Library.Raycast;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public class AttackPointSphere : AttackPoint {



#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if(enabled) {
                Gizmos.color = Color.red;

                if(rayHelper == null) Gizmos.DrawWireSphere(transform.position, m_radius * rayScale);
                else Gizmos.DrawWireSphere(rayHelper.Origin.position, rayHelper.Radius * rayScale);
            }
        }
#endif

        public override void Init(AttackInfo info, int layerMask = -1) {
            attackInfo = info;

            rayHelper = new RaySphereHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                m_radius,
                0.0f,
                layerMask);
        }
    }
}