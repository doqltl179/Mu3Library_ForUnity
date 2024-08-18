using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CameraUtil {
    public class CameraMoveSystem_LocalFollowing : CameraMoveSystem {
        private Transform followingTarget = null;
        /// <summary>
        /// Offset calculated on Local Space.
        /// </summary>
        private Vector3 followingPosOffset = Vector3.zero;
        /// <summary>
        /// Range: 0.0 ~ 1.0
        /// </summary>
        private float followingStrength = 1.0f;



        public override void Move(Camera cam) {
            Vector3 targetPos = followingTarget.transform.TransformPoint(followingPosOffset);

            cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, followingStrength * Time.deltaTime);
        }

        /// <summary>
        /// <br/> 0: Transform
        /// <br/> 1: Vector3
        /// <br/> 2: float
        /// </summary>
        public override void SetProperties(object[] param = null) {
            if(param != null && param.Length >= 3) {
                followingTarget = (Transform)param[0];
                followingPosOffset = (Vector3)param[1];
                followingStrength = (float)param[2];
            }
            else {
                Debug.Log("Camera Setting Properties are not enough.");
            }
        }
    }
}