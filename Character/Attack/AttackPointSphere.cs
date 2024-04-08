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

                Gizmos.DrawWireSphere(transform.position, radius);
            }
        }
#endif

        public override void Init(int layerMask = -1) {
            rayHelper = new RaySphereHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                radius,
                0.0f,
                layerMask);
        }

        protected override void UpdateHit() {
            if(enabled && rayHelper.Raycast()) {
                //hits = locationHit.GetHitPointWithComponentOnRigidbody<CharacterController>();
                hits = rayHelper.GetComponentsOnRigidbody<CharacterController>();
            }
            else {
                hits = null;
            }
        }

        protected override bool IsInHitRange(CharacterController character) {
            return IsHeightInOffsetRange(character);
        }
    }
}