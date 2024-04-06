using Mu3Library.Raycast;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public class AttackPointSphere : AttackPoint {
        private RaySphereHelper locationHit;



#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if(enabled) {
                Gizmos.color = Color.red;

                Gizmos.DrawWireSphere(transform.position, radius);
            }
        }
#endif

        private void Start() {
            locationHit = new RaySphereHelper(
                Coordinate.Local,
                Direction.None,
                transform,
                radius,
                0.0f,
                1 << AttackTargetLayer);
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
    }
}