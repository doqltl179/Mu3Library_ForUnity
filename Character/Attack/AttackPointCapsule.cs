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

        public override void Init(int layerMask = -1) {
            rayHelper = new RayCapsuleHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                radius,
                height,
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