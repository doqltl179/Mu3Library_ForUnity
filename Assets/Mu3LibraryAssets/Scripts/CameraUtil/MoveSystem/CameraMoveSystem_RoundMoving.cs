using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CameraUtil {
    public class CameraMoveSystem_RoundMoving : CameraMoveSystem {
        private Transform followingTarget = null;
        /// <summary>
        /// Offset calculated on Local Space.
        /// </summary>
        private Vector3 followingPosOffset = Vector3.zero;
        /// <summary>
        /// Range: 0.0 ~ 1.0
        /// </summary>
        private float followingStrength = 1.0f;

        private float radius = 1.0f;
        private float sensitive = 10.0f;

        private float currentPosAngleDegX = 0.0f;
        private float currentPosAngleRadX = 0.0f;
        private float currentPosAngleDegY = 0.0f;
        private float currentPosAngleRadY = 0.0f;

        private KeyCode clickKey = KeyCode.Mouse1;
        private Vector3 mousePos;



        public override void Move(Camera cam) {
            Vector3 targetPos = followingTarget.transform.position + followingPosOffset;

#if UNITY_STANDALONE
            if(Input.GetKeyDown(clickKey)) mousePos = Input.mousePosition;
            else if(Input.GetKey(clickKey)) {
                Vector3 mousePosDiff = Input.mousePosition - mousePos;

                currentPosAngleDegX -= mousePosDiff.x * sensitive * Time.deltaTime;
                currentPosAngleDegY -= mousePosDiff.y * sensitive * Time.deltaTime;

                mousePos = Input.mousePosition;
            }
#endif

            currentPosAngleRadX = currentPosAngleDegX * Mathf.Deg2Rad;
            currentPosAngleRadY = currentPosAngleDegY * Mathf.Deg2Rad;

            Vector3 roundPosOffset = new Vector3(
                Mathf.Cos(currentPosAngleRadY) * Mathf.Cos(currentPosAngleRadX),  // X축 움직임
                Mathf.Sin(currentPosAngleRadY),  // Y축 움직임
                Mathf.Cos(currentPosAngleRadY) * Mathf.Sin(currentPosAngleRadX)   // Z축 움직임
            ) * radius;
            Vector3 targetPosToCam = cam.transform.position - targetPos;

            Vector3 camDir = Vector3.Slerp(targetPosToCam.normalized, roundPosOffset.normalized, followingStrength * Time.deltaTime);
            float newRadius = Mathf.Lerp(targetPosToCam.magnitude, roundPosOffset.magnitude, followingStrength * Time.deltaTime);
            cam.transform.position = targetPos + camDir * newRadius;
        }

        /// <summary>
        /// <br/> 0: Transform || followingTarget
        /// <br/> 1: Vector3 || followingPosOffset
        /// <br/> 2: float || followingStrength
        /// <br/> 3: float || radius
        /// <br/> 4: float || sensitive
        /// </summary>
        public override void SetProperties(object[] param = null) {
            if(param != null && param.Length >= 5) {
                followingTarget = (Transform)param[0];
                followingPosOffset = (Vector3)param[1];
                followingStrength = (float)param[2];

                radius = (float)param[3];
                sensitive = (float)param[4];

                currentPosAngleDegX = Mathf.Atan2(followingTarget.forward.z, followingTarget.forward.x);
                currentPosAngleRadX = currentPosAngleDegX * Mathf.Deg2Rad;

                currentPosAngleDegY = 0;
                currentPosAngleRadY = currentPosAngleDegY * Mathf.Deg2Rad;
            }
            else {
                Debug.Log("Camera Setting Properties are not enough.");
            }
        }
    }
}