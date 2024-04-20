using Mu3Library.Scene;
using Mu3Library.Singleton;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library {
    public class CameraManager : GenericSingleton<CameraManager> {
        private Camera camera = null;

        private const float CameraRadius = 0.3f;
        private const float CameraMoveSpeed = 30.0f;
        private const float CameraRotateSpeed = 10.0f;
        private const float ZoomSpeed = 10.0f;

        private const float HeightAngleMin = Mathf.PI * 0.5f * -0.1f - Mathf.PI * 0.5f;
        private const float HeightAngleMax = Mathf.PI * 0.5f * 0.65f - Mathf.PI * 0.5f;
        private readonly float HeightAngleOffsetMin = Mathf.InverseLerp(-Mathf.PI, Mathf.PI, HeightAngleMin);
        private readonly float HeightAngleOffsetMax = Mathf.InverseLerp(-Mathf.PI, Mathf.PI, HeightAngleMax);

        private float cameraHeightAngle;
        private float cameraRotateAngle;
        private float cameraDist;

        private float cameraDistMin;
        private float cameraDistMax;
        private float cameraDistOffset;
        private float currentCameraDist;

        public Transform LookTarget { get; private set; }
        public float TargetRadius { get; private set; }
        private GameObject targetFollower = null;
        private const float TargetLerpStrength = 4.0f;
        private bool cameraLock;

        private float currentHeightAngle;
        private float heightAngleOffset;
        private float currentRotatedAngle;
        private float rotateAngleOffset;
        private const float RotateSpeed = 0.1f;

        public Vector3 CameraForwardXZ { get; private set; }
        public Vector3 CameraRightXZ { get; private set; }
        public float CameraRotatedRadianAngleY { get; private set; }
        public float CameraRotatedDegreeAngleY { get; private set; }

        private KeyCode inputRotate = KeyCode.Mouse1;

        private Vector3 savedMousePos;

        private Ray distRay;
        private RaycastHit distHit;
        private int distRayMask;



        private void Awake() {
            SceneLoader.Instance.OnSceneChanged += OnSceneChanged;

            if(camera == null) {
                camera = Camera.main;
                if(camera == null) {
                    GameObject go = new GameObject("MainCamera");
                    go.tag = "MainCamera";
                    go.AddComponent<AudioListener>();

                    camera = go.AddComponent<Camera>();
                }

                camera.transform.SetParent(transform);
            }

            if(targetFollower == null) {
                targetFollower = new GameObject("TargetFollower");
                targetFollower.transform.SetParent(transform);
            }
        }

        private void OnDestroy() {
            SceneLoader.Instance.OnSceneChanged -= OnSceneChanged;
        }

        private void Update() {
            if(LookTarget != null) {
                targetFollower.transform.position = Vector3.Lerp(targetFollower.transform.position, LookTarget.position, Time.deltaTime * TargetLerpStrength);
                targetFollower.transform.rotation = Quaternion.Lerp(targetFollower.transform.rotation, LookTarget.rotation, Time.deltaTime * TargetLerpStrength);

                if(Input.GetKeyDown(inputRotate)) savedMousePos = Input.mousePosition;
                else if(Input.GetKey(inputRotate)) {
                    Vector3 mousePosDiff = Input.mousePosition - savedMousePos;

                    heightAngleOffset += -mousePosDiff.y * Time.deltaTime * RotateSpeed;
                    if(heightAngleOffset > HeightAngleOffsetMax) heightAngleOffset = HeightAngleOffsetMax;
                    else if(heightAngleOffset < HeightAngleOffsetMin) heightAngleOffset = HeightAngleOffsetMin;

                    rotateAngleOffset += -mousePosDiff.x * Time.deltaTime * RotateSpeed;
                    //if(rotateAngleOffset > 1.0f) rotateAngleOffset -= 1.0f;
                    //else if(rotateAngleOffset < 0.0f) rotateAngleOffset = 1.0f + rotateAngleOffset;

                    savedMousePos = Input.mousePosition;
                }
                currentHeightAngle = Mathf.Lerp(-Mathf.PI, Mathf.PI, heightAngleOffset);
                currentRotatedAngle = Mathf.LerpUnclamped(-Mathf.PI, Mathf.PI, rotateAngleOffset);
                cameraHeightAngle = Mathf.Lerp(cameraHeightAngle, currentHeightAngle, Time.deltaTime * CameraRotateSpeed);
                cameraRotateAngle = Mathf.Lerp(cameraRotateAngle, currentRotatedAngle, Time.deltaTime * CameraRotateSpeed);

                float dx = Mathf.Sin(cameraHeightAngle) * Mathf.Cos(cameraRotateAngle);
                float dy = Mathf.Cos(cameraHeightAngle);
                float dz = Mathf.Sin(cameraHeightAngle) * Mathf.Sin(cameraRotateAngle);
                Vector3 camLocalDirection = new Vector3(dx, dy, dz).normalized;

                Vector3 camWorldDirection = Vector3.zero;
                if(cameraLock) {
                    float targetRotatedAngle = targetFollower.transform.eulerAngles.y;
                    // Degree 각도를 사용해야 함.
                    Quaternion rotateOffset = Quaternion.AngleAxis(targetRotatedAngle, Vector3.up);
                    camWorldDirection = rotateOffset * camLocalDirection;
                }
                else {
                    camWorldDirection = camLocalDirection;
                }

                cameraDistOffset = Mathf.Clamp01(cameraDistOffset + -Input.mouseScrollDelta.y * Time.deltaTime * ZoomSpeed);
                currentCameraDist = Mathf.Lerp(cameraDistMin, cameraDistMax, cameraDistOffset);
                distRay = new Ray(targetFollower.transform.position, camWorldDirection);
                if(Physics.SphereCast(distRay, CameraRadius, out distHit, currentCameraDist, distRayMask)) {
                    currentCameraDist = Vector3.Distance(targetFollower.transform.position, distHit.point);
                }
                cameraDist = Mathf.Lerp(cameraDist, currentCameraDist, Time.deltaTime * CameraMoveSpeed);

                camera.transform.position = targetFollower.transform.position + (camWorldDirection * cameraDist);
                camera.transform.LookAt(targetFollower.transform);

                #region Set Properties
                Vector3 camWorldForward = camera.transform.forward;
                CameraForwardXZ = new Vector3(camWorldForward.x, 0.0f, camWorldForward.z).normalized;
                CameraRightXZ = Quaternion.AngleAxis(90.0f, Vector3.up) * CameraForwardXZ;
                CameraRotatedRadianAngleY = Mathf.Atan2(CameraForwardXZ.z, CameraForwardXZ.x);
                CameraRotatedDegreeAngleY = CameraRotatedRadianAngleY * Mathf.Rad2Deg;
                #endregion
            }
        }



        #region Utility
        public void SetLayerMask(int layerMask) => distRayMask = layerMask;

        public void SetRotateInput(KeyCode input) {
            inputRotate = input;
        }

        public void SetLookTarget(Transform lookTarget, float targetRadius) {
            cameraDistMin = targetRadius * 2.0f;
            cameraDistMax = targetRadius * 5.0f;
            cameraDistOffset = 0.5f;
            currentCameraDist = Mathf.Lerp(cameraDistMin, cameraDistMax, cameraDistOffset);

            heightAngleOffset = (HeightAngleOffsetMax + HeightAngleOffsetMin) * 0.5f;
            currentHeightAngle = Mathf.Lerp(-Mathf.PI, Mathf.PI, heightAngleOffset);

            rotateAngleOffset = Mathf.InverseLerp(-Mathf.PI, Mathf.PI, Mathf.PI * 0.5f);
            currentRotatedAngle = Mathf.Lerp(-Mathf.PI, Mathf.PI, rotateAngleOffset);

            targetFollower.transform.position = lookTarget.position;
            targetFollower.transform.rotation = lookTarget.rotation;

            LookTarget = lookTarget;
            TargetRadius = targetRadius;
        }
        #endregion

        #region Action
        private void OnSceneChanged(SceneType type) {
            Camera[] cams = FindObjectsOfType<Camera>();
            foreach(Camera cam in cams) {
                Destroy(cam.gameObject);
            }
        }
        #endregion
    }
}