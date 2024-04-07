using Mu3Library.Raycast;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public class AttackPointCapsule : AttackPoint {
        private RayCapsuleHelper locationHit;



#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if(enabled) {
                Gizmos.color = Color.red;

                Editor.Gizmo.Draw.WireCapsule(transform, radius, height);
            }
        }
#endif

        private void Start() {
            locationHit = new RayCapsuleHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                radius,
                height,
                0.0f,
                AttackTargetLayer);
        }

        protected override void UpdateHit() {
            if(locationHit.Raycast()) {
                hits = locationHit.GetHitPointWithComponentOnRigidbody<CharacterController>();
            }
            else {
                hits = null;
            }
        }

        protected override bool IsInHitRange(CharacterController character) {
            return true;
        }

        private bool IsHeightInOffsetRange(CharacterController controller) {
            float p1 = transform.position.y;
            float p2 = p1 + height;
            if(p1 < p2) return !(p2 < controller.Pos.y || controller.Pos.y + controller.Height < p1);
            else return !(p1 < controller.Pos.y || controller.Pos.y + controller.Height < p2);
        }
    }
}