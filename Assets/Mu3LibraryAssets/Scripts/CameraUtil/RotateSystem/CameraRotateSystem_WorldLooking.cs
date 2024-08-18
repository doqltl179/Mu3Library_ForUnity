using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CameraUtil {
    public class CameraRotateSystem_WorldLooking : CameraRotateSystem {
        private Transform lookingTarget = null;
        /// <summary>
        /// Offset calculated on Local Space.
        /// </summary>
        private Vector3 lookingPosOffset = Vector3.zero;
        /// <summary>
        /// Range: 0.0 ~ 1.0
        /// </summary>
        private float lookingStrength = 1.0f;



        public override void Rotate(Camera cam) {
            Vector3 targetPos = lookingTarget.transform.position + lookingPosOffset;

            Quaternion lookingRot = Quaternion.LookRotation((targetPos - cam.transform.position).normalized, Vector3.up);

            cam.transform.rotation = Quaternion.Lerp(cam.transform.rotation, lookingRot, lookingStrength * Time.deltaTime);
        }

        /// <summary>
        /// <br/> 0: Transform
        /// <br/> 1: Vector3
        /// <br/> 2: float
        /// </summary>
        public override void SetProperties(object[] param = null) {
            if(param != null && param.Length >= 3) {
                lookingTarget = (Transform)param[0];
                lookingPosOffset = (Vector3)param[1];
                lookingStrength = (float)param[2];
            }
            else {
                Debug.Log("Camera Setting Properties are not enough.");
            }
        }
    }
}